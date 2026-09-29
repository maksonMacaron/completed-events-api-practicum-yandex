# Events API

Система управления мероприятиями и бронированиями на ASP.NET Core. Приложение разделено на три независимых микросервиса с отдельными базами PostgreSQL. Подтверждения броней передаются через Apache Kafka, прямых HTTP-вызовов между сервисами нет.

## Состав системы

| Сервис | Ответственность | База данных | Порт |
| --- | --- | --- | --- |
| Users API | регистрация, вход, хеширование пароля, выдача JWT | `users` | `5001` |
| Events API | CRUD мероприятий, количество доступных мест | `events` | `5002` |
| Bookings API | создание, просмотр, отмена и подтверждение броней | `bookings` | `5003` |

Дополнительные компоненты:

- Kafka доступна с хоста на порту `9092`;
- Zookeeper используется брокером Kafka;
- Redis доступен с хоста на порту `6379`;
- `Shared.Contracts` содержит контракты Kafka, имена топиков, общие Kafka-настройки, инициализатор топиков, JSON-настройки, модель JWT и модели API-ответов.

У каждого сервиса свои проекты `Domain`, `Application`, `Infrastructure` и `Presentation`. Сервисы не используют общую схему БД и не содержат навигационных свойств на сущности соседних сервисов. Связи между пользователем, событием и бронью хранятся только как идентификаторы.

## Поток BookingConfirmed

1. Аутентифицированный пользователь создаёт бронь через `POST /bookings` в Bookings API.
2. Bookings API проверяет наличие события в локальном каталоге `known_events`. Каталог обновляется сообщениями `EventAvailabilityChanged` из Events API, прямой HTTP-вызов не выполняется.
3. Фоновый обработчик в одной транзакции подтверждает ожидающую бронь и добавляет `BookingConfirmed` в таблицу `outbox_messages`.
4. Отдельный outbox-worker публикует сообщение в топик `booking-confirmed` и отмечает его отправленным. В качестве ключа используется `EventId`, поэтому сообщения одного события сохраняют порядок внутри партиции.
5. Events API читает сообщение в группе `events-service`, создаёт отдельный DI scope и уменьшает `AvailableSeats` у нужного события.
6. `BookingId` записывается в таблицу `processed_booking_messages` в одной транзакции с изменением события. Повторная доставка того же сообщения не уменьшает количество мест ещё раз.

Если событие не найдено или мест недостаточно, Events API не помечает сообщение обработанным, не фиксирует офсет и повторяет попытку с задержкой. Оба сервиса при старте пытаются создать необходимые топики через Kafka Admin API; существующий топик считается нормальным состоянием.

Изменение брони и создание outbox-сообщения атомарны: состояние, для которого не запланирована публикация, в базе появиться не может. Доставка остаётся `at-least-once`: при остановке после отправки в Kafka, но до отметки `published_at`, сообщение может уйти повторно. Идемпотентный обработчик Events API безопасно пропустит такой дубликат.

Несколько экземпляров фонового обработчика атомарно забирают разные порции броней через `FOR UPDATE SKIP LOCKED`. Транзакция удерживает блокировку до одновременной записи статуса и outbox. При простое интервал опроса постепенно увеличивается с одной до тридцати секунд.

## Проверка существования события

Events API публикует `EventAvailabilityChanged` при создании и удалении мероприятия, а при старте отправляет снимок идентификаторов уже существующих событий. До начала приёма запросов Bookings API перечитывает накопленные события каталога с начала топика, затем продолжает работу в группе `bookings-service`. Локально хранятся только идентификатор, признак доступности и время изменения. Удалённые записи очищаются через 30 дней. Неизвестный или удалённый `EventId` возвращает `404`, при этом сервисы по-прежнему не обращаются друг к другу по HTTP.

## Поток BookingCancelled

При отмене подтверждённой брони Bookings API блокирует строку через `FOR UPDATE` и в одной транзакции сохраняет статус `Cancelled` вместе с `BookingCancelled` в outbox. Поэтому параллельные DELETE и worker не могут перезаписать состояние друг друга или создать две отмены. Events API возвращает места через `Event.ReleaseSeats` и в той же транзакции записывает `BookingId` в `processed_booking_cancellations`.

Outbox-worker публикует сообщения одной брони по порядку их идентификаторов, поэтому `BookingConfirmed` всегда отправляется раньше `BookingCancelled`. Если событие отмены всё же пришло раньше подтверждения, его офсет не фиксируется: обработчик повторит попытку после обработки `BookingConfirmed`. Повторная доставка обоих сообщений безопасна.

## Кеширование событий

Events API использует Redis как общий кеш для всех экземпляров сервиса и применяет паттерн Cache-Aside.

- `GET /events/{id}` сначала ищет событие по ключу `event:{id}`. При промахе данные загружаются из PostgreSQL и сохраняются на 5 минут. После создания, обновления или удаления события ключ инвалидируется только после успешной записи в базу. Такой порядок сохраняет базу источником актуальных данных, а TTL служит дополнительной защитой, если удалить ключ не удалось.
- `GET /events/top` возвращает десять событий с наибольшей долей проданных мест: `(total_seats - available_seats) / total_seats`. Список хранится по ключу `events:top10` в течение 10 минут. Для рейтингового агрегата небольшая задержка обновления допустима, поэтому он обновляется только по TTL без инвалидации при каждом бронировании.

Подтверждение или отмена брони через Kafka изменяет количество доступных мест. После успешной записи обработчик удаляет `event:{id}`, поэтому следующий запрос получает актуальное событие из базы. Ключ топа при этом не удаляется и продолжает жить до истечения своего TTL.

Соединение `ConnectionMultiplexer` зарегистрировано как singleton. Если Redis временно недоступен, операции кеша записывают предупреждение в журнал и не прерывают запрос: чтение продолжается из PostgreSQL. Значения TTL и строка подключения находятся в секции `Redis` файла конфигурации и могут быть переопределены переменными окружения.

## JWT и права доступа

JWT выдаёт только Users API через `POST /auth/login`. Все сервисы используют одинаковые `Secret`, `Issuer` и `Audience`, поэтому Events API и Bookings API могут проверить выданный токен самостоятельно.

- `GET /events`, `GET /events/{id}` и `GET /events/top` доступны без токена;
- `POST /events`, `PUT /events/{id}` и `DELETE /events/{id}` доступны только роли `Admin`;
- все эндпоинты `/bookings` требуют аутентификации;
- пользователь может отменить только свою бронь, администратор — любую.

Для учебного сценария роль можно передать при регистрации. Если поле `role` не указано, создаётся пользователь с ролью `User`.

## Запуск через Docker Compose

Требуется Docker с поддержкой Compose.

```bash
docker compose up --build
```

После запуска доступны Swagger UI:

- Users API: <http://localhost:5001/swagger>
- Events API: <http://localhost:5002/swagger>
- Bookings API: <http://localhost:5003/swagger>

Каждый сервис при старте применяет миграции только к своей базе данных.

Остановить систему:

```bash
docker compose down
```

Удалить также учебные данные из именованных volumes:

```bash
docker compose down -v
```

## Проверка полного сценария

### 1. Зарегистрировать администратора

```http
POST http://localhost:5001/auth/register
Content-Type: application/json

{
  "login": "admin",
  "password": "strong-password",
  "role": "Admin"
}
```

### 2. Получить токен

```http
POST http://localhost:5001/auth/login
Content-Type: application/json

{
  "login": "admin",
  "password": "strong-password"
}
```

Скопируйте `data.token` из ответа и передавайте его как `Authorization: Bearer <token>`.

### 3. Создать событие

```http
POST http://localhost:5002/events
Authorization: Bearer <token>
Content-Type: application/json

{
  "title": "Концерт",
  "description": "Большой зал",
  "startAt": "2030-10-01T18:00:00Z",
  "endAt": "2030-10-01T20:00:00Z",
  "totalSeats": 100
}
```

Скопируйте `data.id` из ответа.

Дождитесь обработки `EventAvailabilityChanged` сервисом Bookings. Обычно локальный каталог обновляется сразу; при слишком быстром запросе бронирования повторите его через секунду.

### 4. Создать бронь

```http
POST http://localhost:5003/bookings
Authorization: Bearer <token>
Content-Type: application/json

{
  "eventId": "<event-id>",
  "seats": 2
}
```

Bookings API вернёт `202 Accepted`. Через несколько секунд `GET /bookings/{id}` покажет статус `Confirmed`.

### 5. Проверить количество мест

```http
GET http://localhost:5002/events/<event-id>
```

Поле `availableSeats` уменьшится на два после обработки события из Kafka.

## Локальный запуск без Docker для приложений

Для полного локального сценария PostgreSQL, Kafka и Redis запускаются отдельно. Локальные строки подключения находятся в `appsettings.Development.json`; Kafka по умолчанию ожидается на `localhost:9092`, Redis — на `localhost:6379`. Без Redis Events API продолжает читать данные из PostgreSQL, но не использует кеш.

```bash
dotnet run --project src/UsersAPI.Presentation/UsersAPI.Presentation.csproj
dotnet run --project src/EventsAPI.Presentation/EventsAPI.Presentation.csproj
dotnet run --project src/BookingsAPI.Presentation/BookingsAPI.Presentation.csproj
```

Настройки можно переопределить переменными окружения:

- `ConnectionStrings__DefaultConnection`;
- `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`;
- `Kafka__BootstrapServers`;
- `Kafka__ConsumerGroup` для Events API и Bookings API.
- `Redis__ConnectionString`, `Redis__EventTtlMinutes`, `Redis__TopEventsTtlMinutes` для Events API.

## Миграции

У каждого сервиса отдельная начальная миграция EF Core:

- `UsersAPI.Infrastructure/Persistence/Migrations`;
- `EventsAPI.Infrastructure/Persistence/Migrations`;
- `BookingsAPI.Infrastructure/Persistence/Migrations`.

Пример создания следующей миграции Events API:

```bash
dotnet ef migrations add MigrationName \
  --project src/EventsAPI.Infrastructure/EventsAPI.Infrastructure.csproj \
  --startup-project src/EventsAPI.Presentation/EventsAPI.Presentation.csproj \
  --output-dir Persistence/Migrations
```

## Сборка

```bash
dotnet restore EventsAPI.slnx
dotnet build EventsAPI.slnx --no-restore
dotnet test EventsAPI.slnx --no-build
```

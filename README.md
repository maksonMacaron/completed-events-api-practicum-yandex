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
- `Shared.Contracts` содержит контракты Kafka, имена топиков, общую модель JWT-настроек и модели API-ответов.

У каждого сервиса свои проекты `Domain`, `Application`, `Infrastructure` и `Presentation`. Сервисы не используют общую схему БД и не содержат навигационных свойств на сущности соседних сервисов. Связи между пользователем, событием и бронью хранятся только как идентификаторы.

## Поток BookingConfirmed

1. Аутентифицированный пользователь создаёт бронь через `POST /bookings` в Bookings API.
2. Bookings API проверяет наличие события в локальном каталоге `known_events`. Каталог обновляется сообщениями `EventAvailabilityChanged` из Events API, прямой HTTP-вызов не выполняется.
3. Фоновый обработчик подтверждает ожидающую бронь и сначала сохраняет новый статус в базе Bookings.
4. Bookings API публикует `BookingConfirmed` в топик `booking-confirmed`. В качестве ключа сообщения используется `EventId`, поэтому сообщения одного события сохраняют порядок внутри партиции.
5. Events API читает сообщение в группе `events-service`, создаёт отдельный DI scope и уменьшает `AvailableSeats` у нужного события.
6. `BookingId` записывается в таблицу `processed_booking_messages` в одной транзакции с изменением события. Повторная доставка того же сообщения не уменьшает количество мест ещё раз.

Если событие не найдено или мест недостаточно, Events API не помечает сообщение обработанным, не фиксирует офсет и повторяет попытку с задержкой. Оба сервиса при старте пытаются создать необходимые топики через Kafka Admin API; существующий топик считается нормальным состоянием.

После успешной публикации Bookings API сохраняет время отправки. Если процесс остановится между публикацией и этой записью, сообщение может уйти повторно, но идемпотентный обработчик Events API безопасно пропустит дубликат.

Несколько экземпляров фонового обработчика атомарно забирают разные порции броней через `FOR UPDATE SKIP LOCKED`. Временная блокировка записи автоматически истекает, поэтому бронь не останется необработанной после аварийной остановки экземпляра.

## Проверка существования события

Events API публикует `EventAvailabilityChanged` при создании и удалении мероприятия, а при старте отправляет снимок идентификаторов уже существующих событий. Bookings API читает эти сообщения отдельной группой `bookings-service` и хранит только идентификатор, признак доступности и время изменения. При создании брони неизвестный или удалённый `EventId` возвращает `404`, при этом сервисы по-прежнему не обращаются друг к другу по HTTP.

## Поток BookingCancelled

При отмене подтверждённой брони Bookings API сначала сохраняет статус `Cancelled`, затем публикует `BookingCancelled` в топик `booking-cancelled`. Events API возвращает места через `Event.ReleaseSeats` и в той же транзакции записывает `BookingId` в `processed_booking_cancellations`.

Если бронь отменена после сохранения статуса `Confirmed`, но до фиксации публикации подтверждения, worker сначала публикует `BookingConfirmed` и только затем `BookingCancelled`. Если событие отмены всё же пришло раньше подтверждения, его офсет не фиксируется: обработчик повторит попытку после обработки `BookingConfirmed`. Повторная доставка обоих сообщений безопасна. Если публикация отмены прервалась, worker Bookings API подберёт отменённую бронь без отметки о публикации и отправит сообщение повторно.

## JWT и права доступа

JWT выдаёт только Users API через `POST /auth/login`. Все сервисы используют одинаковые `Secret`, `Issuer` и `Audience`, поэтому Events API и Bookings API могут проверить выданный токен самостоятельно.

- `GET /events` и `GET /events/{id}` доступны без токена;
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

PostgreSQL и Kafka должны быть запущены отдельно. Локальные строки подключения находятся в `appsettings.Development.json`; Kafka по умолчанию ожидается на `localhost:9092`.

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
```

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
- `Shared.Contracts` содержит контракт `BookingConfirmed` и имя топика `booking-confirmed`.

У каждого сервиса свои проекты `Domain`, `Application`, `Infrastructure` и `Presentation`. Сервисы не используют общую схему БД и не содержат навигационных свойств на сущности соседних сервисов. Связи между пользователем, событием и бронью хранятся только как идентификаторы.

## Поток BookingConfirmed

1. Аутентифицированный пользователь создаёт бронь через `POST /bookings` в Bookings API.
2. Фоновый обработчик подтверждает ожидающую бронь и сначала сохраняет новый статус в базе Bookings.
3. Bookings API публикует `BookingConfirmed` в топик `booking-confirmed`. В качестве ключа сообщения используется `EventId`, поэтому сообщения одного события сохраняют порядок внутри партиции.
4. Events API читает сообщение в группе `events-service`, создаёт отдельный DI scope и уменьшает `AvailableSeats` у нужного события.
5. `BookingId` записывается в таблицу `processed_booking_messages` в одной транзакции с изменением события. Повторная доставка того же сообщения не уменьшает количество мест ещё раз.

Если событие не найдено или мест недостаточно, Events API явно записывает предупреждение в лог и продолжает чтение. При временной ошибке обработки офсет не фиксируется, сообщение будет прочитано повторно. Events API при старте пытается создать топик через Kafka Admin API; существующий топик считается нормальным состоянием.

После успешной публикации Bookings API сохраняет время отправки. Если процесс остановится между публикацией и этой записью, сообщение может уйти повторно, но идемпотентный обработчик Events API безопасно пропустит дубликат.

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
- `Kafka__ConsumerGroup` для Events API.

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

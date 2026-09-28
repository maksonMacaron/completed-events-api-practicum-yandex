# Events API (ASP.NET Core Web API)

Простой REST API для управления мероприятиями.

---

## 🚀 Возможности

* Получение списка событий с фильтрацией и пагинацией
* Получение события по Id
* Создание события
* Обновление события
* Удаление события
* Регистрация пользователей и вход по JWT
* Разграничение доступа между ролями `Admin` и `User`
* Создание брони с быстрым ответом `202 Accepted`
* Получение текущего состояния брони
* Отмена своей брони пользователем или любой брони администратором
* Подтверждение ожидающих броней фоновым сервисом
* Ограничение количества мест и защита от конкурентного овербукинга
* Запрет бронирования начавшихся событий и лимит в 10 активных броней на пользователя

---

## 🧱 Технологии

* C#
* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL
* EF Core Migrations
* Testcontainers for .NET
* Swagger (OpenAPI)
* xUnit

---

## 📦 Структура проекта

Приложение разделено на четыре сборки по принципам Clean Architecture:

* `EventsAPI.Domain` — сущности `Event`, `Booking` и `User`, доменные правила и исключения. Слой не зависит от остальных проектов;
* `EventsAPI.Application` — сценарии приложения, DTO и интерфейсы портов для репозиториев. Зависит только от `Domain`;
* `EventsAPI.Infrastructure` — `AppDbContext`, EF Core-конфигурации, миграции, реализации репозиториев и фоновый hosted-адаптер. Зависит от `Application` и `Domain`;
* `EventsAPI.Presentation` — контроллеры, HTTP-контракты, глобальная обработка ошибок и composition root. Зависит от `Application` и `Infrastructure`.

Направление зависимостей:

```text
Presentation -> Application -> Domain
       |              ^
       v              |
Infrastructure -------+
       |
       +---------------------> Domain
```

Интерфейсы репозиториев объявлены в `Application`, а их EF Core-реализации находятся в `Infrastructure`. Поэтому сценарии приложения не зависят от способа хранения данных.

Тестовые проекты:

* `EventService.Tests` — unit-тесты домена и сценариев приложения с EF Core InMemory;
* `EventApi.IntegrationTests` — интеграционные тесты репозиториев и миграций с PostgreSQL;
* `EventApi.HttpTests` — HTTP-тесты аутентификации, авторизации и контрактов API.

---

## ▶️ Запуск проекта

### 1. Клонировать репозиторий

```bash
git clone https://github.com/maksonMacaron/events-api-practicum-yandex
cd events-api-practicum-yandex
```

### 2. Подготовить PostgreSQL

Для локального запуска необходим доступный экземпляр PostgreSQL. Среда `Development` использует строку подключения из `src/EventsAPI.Presentation/appsettings.Development.json`:

```text
Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres
```

В базовом `appsettings.json` строка подключения оставлена пустой. Для других окружений её необходимо задать через `ConnectionStrings__DefaultConnection`, чтобы production-учётные данные не хранились в репозитории.

Схема базы (`events`, `bookings` и `users`) управляется миграциями EF Core. При запуске приложение вызывает `Database.Migrate()` и автоматически применяет ещё не выполненные миграции. Логин пользователя уникален, а каждая бронь связана с владельцем через `UserId`.

Если база была создана в предыдущей версии приложения через `EnsureCreated()`, удалите её перед первым запуском этой версии. После этого таблицы будут созданы начальной миграцией.

### 3. Запустить проект

```bash
dotnet run --project src/EventsAPI.Presentation/EventsAPI.Presentation.csproj --launch-profile http
```

### Миграции

Создать миграцию после изменения модели:

```bash
dotnet ef migrations add MigrationName \
  --project src/EventsAPI.Infrastructure/EventsAPI.Infrastructure.csproj \
  --startup-project src/EventsAPI.Presentation/EventsAPI.Presentation.csproj \
  --output-dir Persistence/Migrations
```

Применить миграции вручную:

```bash
dotnet ef database update \
  --project src/EventsAPI.Infrastructure/EventsAPI.Infrastructure.csproj \
  --startup-project src/EventsAPI.Presentation/EventsAPI.Presentation.csproj
```

Для выполнения этих команд нужен инструмент `dotnet-ef` версии, совместимой с EF Core проекта.

---

## 📘 Swagger

После запуска открой:

`http://localhost:<port>/swagger`

Точный адрес и порт выводятся командой `dotnet run` в строке `Now listening on`.

### Получение JWT-токена

1. Выполните `POST /auth/register`:

   ```json
   {
     "login": "admin",
     "password": "strong-password",
     "role": "Admin"
   }
   ```

   Поле `role` необязательно: без него пользователь получит роль `User`.

2. Выполните `POST /auth/login` с тем же логином и паролем:

   ```json
   {
     "login": "admin",
     "password": "strong-password"
   }
   ```

3. Скопируйте значение `data.token` из ответа.
4. Нажмите кнопку **Authorize** в Swagger и вставьте токен. После этого Swagger будет отправлять его в заголовке `Authorization: Bearer <token>`.

### Настройка JWT

Параметры JWT находятся в секции `Jwt` файла `appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "replace-with-at-least-32-characters",
    "Issuer": "EventsAPI",
    "Audience": "EventsAPI.Client",
    "LifetimeMinutes": 60
  }
}
```

Значение в репозитории предназначено только для локальной разработки. В production задайте длинный случайный секрет через безопасное хранилище конфигурации, например переменную окружения `Jwt__Secret`, и не сохраняйте рабочий ключ в репозитории.

---

## 🔐 Роли и права

* `Admin` может создавать, изменять и удалять события, бронировать события и отменять любую бронь.
* `User` может просматривать события, создавать брони и отменять только собственные брони.
* `POST /events`, `PUT /events/{id}` и `DELETE /events/{id}` доступны только администратору.
* `POST /events/{id}/book`, `GET /bookings/{id}` и `DELETE /bookings/{id}` требуют JWT-токен.

Запрос без токена к защищённому эндпоинту получает `401 Unauthorized`. Аутентифицированный пользователь без нужной роли или без права на отмену чужой брони получает `403 Forbidden`.

---

## 🔍 Фильтрация и пагинация

GET /events

### Query-параметры:

- title — поиск по названию (регистронезависимый)
- from — дата начала
- to — дата окончания
- page — номер страницы (по умолчанию 1)
- pageSize — размер страницы (по умолчанию 10)

Пример:

`GET /events?title=концерт&from=2026-06-01&page=1&pageSize=5`

---

## 📄 Ответ

```json
{
  "data": {
    "page": 1,
    "pageSize": 5,
    "total": 12,
    "count": 5,
    "items": []
  },
  "success": true,
  "statusCode": 200,
  "message": "Список событий"
}
```

---

## 🎟️ Бронирования

Каждое событие содержит:

* `TotalSeats` — общее количество мест, обязательное при создании и больше нуля;
* `AvailableSeats` — текущее количество свободных мест, изначально равное `TotalSeats`.

`Booking` содержит `Id` брони, `EventId` мероприятия, `UserId` владельца, `Status`, время создания `CreatedAt` в UTC и время обработки `ProcessedAt` в UTC. До обработки `ProcessedAt` равен `null`.

Статусы:

* `Pending` — бронь создана и ожидает обработки;
* `Confirmed` — бронь подтверждена;
* `Rejected` — бронь отклонена;
* `Cancelled` — бронь отменена владельцем или администратором.

### POST /events/{id}/book

Создаёт бронь для существующего мероприятия и уменьшает `AvailableSeats` на единицу. Эндпоинт требует JWT-токен и берёт `UserId` из claims. Отвечает `202 Accepted`, возвращает бронь со статусом `Pending` в поле `data` и адрес для проверки состояния в заголовке `Location`. Если мероприятие не найдено, возвращает `404`. Для уже начавшегося события возвращается `400`. Если свободные места закончились или у пользователя уже есть 10 активных броней, возвращается `409 Conflict`.

### GET /bookings/{id}

Возвращает текущее состояние брони с кодом `200 OK`. Требует JWT-токен. Если бронь не найдена, возвращает `404`.

### DELETE /bookings/{id}

Отменяет бронь и возвращает место событию. Владелец может отменить свою бронь, администратор — любую. Попытка обычного пользователя отменить чужую бронь возвращает `403 Forbidden`.

Сервис раз в секунду проверяет ожидающие брони. Имитация внешнего вызова выполняется параллельно, но одновременно обрабатывается не больше десяти броней, чтобы не исчерпать пул подключений PostgreSQL. Затем заявка получает статус `Confirmed` и время `ProcessedAt`. Если обработка завершилась ошибкой, бронь получает статус `Rejected`, а зарезервированное место возвращается. Брони и мероприятия хранятся в PostgreSQL и сохраняются после перезапуска приложения.

Создание брони защищено отдельным `SemaphoreSlim` для каждого события: запросы к одному событию выполняются последовательно, а бронирования разных событий не блокируют друг друга. Фоновый singleton-сервис получает scoped-контексты через `IServiceScopeFactory`; для каждой параллельно обрабатываемой брони создаётся отдельный scope.

### Пример через Swagger

1. Зарегистрируйтесь и авторизуйтесь как `Admin`, затем выполните `POST /events` с телом:

   ```json
   {
     "title": "Концерт",
     "startAt": "2030-10-01T18:00:00Z",
     "endAt": "2030-10-01T20:00:00Z",
     "totalSeats": 3
   }
   ```

2. Скопируйте `data.id` созданного мероприятия. При необходимости войдите как обычный пользователь и трижды выполните `POST /events/{id}/book`. Каждый запрос вернёт `202 Accepted`.
3. Выполните запрос бронирования в четвёртый раз. API вернёт `409 Conflict`, потому что свободных мест больше нет.
4. Убедитесь, что заголовки `Location` успешных ответов указывают на `/bookings/{bookingId}`, а `data.status` равен `Pending`.
5. Откройте `GET /bookings/{bookingId}` сразу после создания, затем повторите запрос через несколько секунд. Статус изменится на `Confirmed`, а `processedAt` получит время обработки.

---

## ❌ Ошибки

Если событие или бронь не найдены, соответствующий эндпоинт возвращает `404`:

```json
{
  "success": false,
  "statusCode": 404,
  "message": "Событие по Id [...] не найдено"
}
```

---

## 🧪 Тесты

В решении есть три тестовых проекта:

* `EventService.Tests` содержит быстрые unit-тесты доменных правил и Application-сервисов;
* `EventApi.IntegrationTests` проверяет миграции и реализации репозиториев на настоящей PostgreSQL;
* `EventApi.HttpTests` проверяет HTTP-коды, JWT-аутентификацию и ролевую авторизацию.

Интеграционные тесты используют одну PostgreSQL в Testcontainers. Перед каждым тестом база пересоздаётся и к ней заново применяются миграции, поэтому тесты не зависят друг от друга или от порядка запуска. Порт и строка подключения выдаются Testcontainers, захардкоженных настроек подключения нет.

Перед запуском всех тестов запустите Docker. Затем выполните:

```bash
dotnet test EventsAPI.slnx
```

Запустить только unit-тесты можно без Docker:

```bash
dotnet test EventService.Tests/EventService.Tests.csproj
```

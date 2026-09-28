# Events API (ASP.NET Core Web API)

Простой REST API для управления мероприятиями.

---

## 🚀 Возможности

* Получение списка событий с фильтрацией и пагинацией
* Получение события по Id
* Создание события
* Обновление события
* Удаление события
* Создание брони с быстрым ответом `202 Accepted`
* Получение текущего состояния брони
* Подтверждение ожидающих броней фоновым сервисом
* Ограничение количества мест и защита от конкурентного овербукинга

---

## 🧱 Технологии

* C#
* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL
* EF Core Migrations
* Testcontainers for .NET
* AutoMapper
* Swagger (OpenAPI)
* xUnit

---

## 📦 Структура проекта

* Controllers — обработка HTTP-запросов
* DataAccess — `AppDbContext`, миграции, репозитории и Fluent API-конфигурации сущностей
* Services — бизнес-логика
* Models — доменные модели
* DTOs — модели для API
* Contracts/Responses — ответы API
* Mapping — профили AutoMapper
* Middlewares — глобальная обработка ошибок
* EventService.Tests — юнит-тесты
* EventApi.IntegrationTests — интеграционные тесты репозиториев с PostgreSQL

---

## ▶️ Запуск проекта

### 1. Клонировать репозиторий

```bash
git clone https://github.com/maksonMacaron/events-api-practicum-yandex
cd events-api-practicum-yandex
```

### 2. Подготовить PostgreSQL

Для локального запуска необходим доступный экземпляр PostgreSQL. Среда `Development` использует строку подключения из `src/EventsAPI/appsettings.Development.json`:

```text
Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres
```

В базовом `appsettings.json` строка подключения оставлена пустой. Для других окружений её необходимо задать через `ConnectionStrings__DefaultConnection`, чтобы production-учётные данные не хранились в репозитории.

Схема базы (`events` и `bookings`) управляется миграциями EF Core. При запуске приложение вызывает `Database.Migrate()` и автоматически применяет ещё не выполненные миграции.

Если база была создана в предыдущей версии приложения через `EnsureCreated()`, удалите её перед первым запуском этой версии. После этого таблицы будут созданы начальной миграцией.

### 3. Запустить проект

```bash
dotnet run --project src/EventsAPI/EventsAPI.csproj --launch-profile http
```

### Миграции

Создать миграцию после изменения модели:

```bash
dotnet ef migrations add MigrationName --project src/EventsAPI/EventsAPI.csproj --startup-project src/EventsAPI/EventsAPI.csproj --output-dir DataAccess/Migrations
```

Применить миграции вручную:

```bash
dotnet ef database update --project src/EventsAPI/EventsAPI.csproj --startup-project src/EventsAPI/EventsAPI.csproj
```

Для выполнения этих команд нужен инструмент `dotnet-ef` версии, совместимой с EF Core проекта.

---

## 📘 Swagger

После запуска открой:

`http://localhost:<port>/swagger`

Точный адрес и порт выводятся командой `dotnet run` в строке `Now listening on`.

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

`Booking` содержит `Id` брони, `EventId` мероприятия, `Status`, время создания `CreatedAt` в UTC и время обработки `ProcessedAt` в UTC. До обработки `ProcessedAt` равен `null`.

Статусы:

* `Pending` — бронь создана и ожидает обработки;
* `Confirmed` — бронь подтверждена;
* `Rejected` — бронь отклонена. Этот статус предусмотрен моделью; текущий фоновый сервис подтверждает все ожидающие брони.

### POST /events/{id}/book

Создаёт бронь для существующего мероприятия и уменьшает `AvailableSeats` на единицу. Отвечает `202 Accepted`, возвращает бронь со статусом `Pending` в поле `data` и адрес для проверки состояния в заголовке `Location`. Если мероприятие не найдено, возвращает `404`. Если свободные места закончились, возвращает `409 Conflict` с сообщением `No available seats for this event`.

### GET /bookings/{id}

Возвращает текущее состояние брони с кодом `200 OK`. Если бронь не найдена, возвращает `404`.

Сервис раз в секунду проверяет ожидающие брони. Имитация внешнего вызова выполняется параллельно, но одновременно обрабатывается не больше десяти броней, чтобы не исчерпать пул подключений PostgreSQL. Затем заявка получает статус `Confirmed` и время `ProcessedAt`. Если обработка завершилась ошибкой, бронь получает статус `Rejected`, а зарезервированное место возвращается. Брони и мероприятия хранятся в PostgreSQL и сохраняются после перезапуска приложения.

Создание брони защищено отдельным `SemaphoreSlim` для каждого события: запросы к одному событию выполняются последовательно, а бронирования разных событий не блокируют друг друга. Фоновый singleton-сервис получает scoped-контексты через `IServiceScopeFactory`; для каждой параллельно обрабатываемой брони создаётся отдельный scope.

### Пример через Swagger

1. Выполните `POST /events` с телом:

   ```json
   {
     "title": "Концерт",
     "startAt": "2026-10-01T18:00:00Z",
     "endAt": "2026-10-01T20:00:00Z",
     "totalSeats": 3
   }
   ```

2. Скопируйте `data.id` созданного мероприятия и трижды выполните `POST /events/{id}/book`. Каждый запрос вернёт `202 Accepted`.
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

В решении есть два тестовых проекта:

* `EventService.Tests` содержит быстрые unit-тесты на EF Core InMemory;
* `EventApi.IntegrationTests` проверяет миграцию и все методы репозиториев на настоящей PostgreSQL.

Интеграционные тесты используют одну PostgreSQL в Testcontainers. Перед каждым тестом база пересоздаётся и к ней заново применяются миграции, поэтому тесты не зависят друг от друга или от порядка запуска. Порт и строка подключения выдаются Testcontainers, захардкоженных настроек подключения нет.

Перед запуском всех тестов запустите Docker. Затем выполните:

```bash
dotnet test EventsAPI.slnx
```

Запустить только unit-тесты можно без Docker:

```bash
dotnet test EventService.Tests/EventService.Tests.csproj
```

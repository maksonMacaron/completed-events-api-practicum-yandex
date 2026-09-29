using EventsAPI.Application.Abstractions.Caching;
using EventsAPI.Application.Abstractions.Messaging;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Caching;
using EventsAPI.Application.DTOs;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using Shared.Contracts;

namespace Services.Tests;

public sealed class EventServiceCachingTests
{
    private static readonly EventCacheOptions CacheOptions = new()
    {
        EventTimeToLive = TimeSpan.FromMinutes(5),
        TopEventsTimeToLive = TimeSpan.FromMinutes(10)
    };

    [Fact]
    public async Task GetByIdAsync_ReturnsCachedEventWithoutRepositoryCall()
    {
        var eventDto = CreateEventDto();
        var repository = new EventRepositoryStub();
        var cache = new CacheStub();
        cache.Values[EventCacheKeys.ById(eventDto.Id)] = eventDto;
        var service = CreateService(repository, cache);

        var result = await service.GetByIdAsync(eventDto.Id);

        Assert.Same(eventDto, result);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_LoadsMissingEventAndStoresItInCache()
    {
        var eventItem = CreateEvent();
        var repository = new EventRepositoryStub { EventToReturn = eventItem };
        var cache = new CacheStub();
        var service = CreateService(repository, cache);

        var result = await service.GetByIdAsync(eventItem.Id);

        Assert.Equal(eventItem.Id, result.Id);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.IsType<EventDto>(cache.Values[EventCacheKeys.ById(eventItem.Id)]);
        Assert.Equal(CacheOptions.EventTimeToLive, cache.LastTimeToLive);
    }

    [Fact]
    public async Task GetTopAsync_ReturnsCachedEventsWithoutRepositoryCall()
    {
        var cachedEvents = new List<EventDto> { CreateEventDto() };
        var repository = new EventRepositoryStub();
        var cache = new CacheStub();
        cache.Values[EventCacheKeys.TopEvents] = cachedEvents;
        var service = CreateService(repository, cache);

        var result = await service.GetTopAsync();

        Assert.Same(cachedEvents, result);
        Assert.Equal(0, repository.GetTopCallCount);
    }

    [Fact]
    public async Task GetTopAsync_LoadsMissingEventsAndStoresThemInCache()
    {
        var eventItem = CreateEvent();
        var repository = new EventRepositoryStub { TopEvents = [eventItem] };
        var cache = new CacheStub();
        var service = CreateService(repository, cache);

        var result = await service.GetTopAsync();

        Assert.Single(result);
        Assert.Equal(1, repository.GetTopCallCount);
        Assert.IsType<List<EventDto>>(cache.Values[EventCacheKeys.TopEvents]);
        Assert.Equal(CacheOptions.TopEventsTimeToLive, cache.LastTimeToLive);
    }

    [Fact]
    public async Task CreateEventAsync_InvalidatesEventAfterDatabaseWrite()
    {
        var operations = new List<string>();
        var repository = new EventRepositoryStub { Operations = operations };
        var cache = new CacheStub { Operations = operations };
        var service = CreateService(repository, cache);
        var request = new CreateEvent
        {
            Title = "Концерт",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(2),
            TotalSeats = 100
        };

        var result = await service.CreateEventAsync(request);

        Assert.Equal(["database", "cache"], operations);
        Assert.Contains(EventCacheKeys.ById(result.Id), cache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesEventAfterDatabaseWrite()
    {
        var eventItem = CreateEvent();
        var operations = new List<string>();
        var repository = new EventRepositoryStub
        {
            EventToReturn = eventItem,
            Operations = operations
        };
        var cache = new CacheStub { Operations = operations };
        var service = CreateService(repository, cache);
        var update = CreateEventDto(eventItem.Id);
        update.Title = "Обновлённый концерт";

        await service.UpdateAsync(eventItem.Id, update);

        Assert.Equal(["database", "cache"], operations);
        Assert.Contains(EventCacheKeys.ById(eventItem.Id), cache.RemovedKeys);
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesEventAfterDatabaseWrite()
    {
        var eventItem = CreateEvent();
        var operations = new List<string>();
        var repository = new EventRepositoryStub
        {
            EventToReturn = eventItem,
            Operations = operations
        };
        var cache = new CacheStub { Operations = operations };
        var service = CreateService(repository, cache);

        await service.DeleteAsync(eventItem.Id);

        Assert.Equal(["database", "cache"], operations);
        Assert.Contains(EventCacheKeys.ById(eventItem.Id), cache.RemovedKeys);
    }

    [Fact]
    public async Task BookingConfirmedHandler_InvalidatesChangedEvent()
    {
        var eventId = Guid.NewGuid();
        var repository = new EventRepositoryStub
        {
            ConfirmationResult = BookingConfirmationResult.Applied
        };
        var cache = new CacheStub();
        var handler = new BookingConfirmedHandler(repository, cache);
        var message = new BookingConfirmed(
            Guid.NewGuid(),
            eventId,
            Guid.NewGuid(),
            2,
            DateTime.UtcNow);

        await handler.HandleAsync(message);

        Assert.Contains(EventCacheKeys.ById(eventId), cache.RemovedKeys);
    }

    [Fact]
    public async Task BookingCancelledHandler_InvalidatesChangedEvent()
    {
        var eventId = Guid.NewGuid();
        var repository = new EventRepositoryStub
        {
            CancellationResult = BookingCancellationResult.Released
        };
        var cache = new CacheStub();
        var handler = new BookingCancelledHandler(repository, cache);
        var message = new BookingCancelled(
            Guid.NewGuid(),
            eventId,
            Guid.NewGuid(),
            2,
            DateTime.UtcNow);

        await handler.HandleAsync(message);

        Assert.Contains(EventCacheKeys.ById(eventId), cache.RemovedKeys);
    }

    private static EventService CreateService(
        IEventRepository repository,
        ICacheService cache) =>
        new(
            repository,
            new EventAvailabilityPublisherStub(),
            cache,
            CacheOptions,
            TimeProvider.System);

    private static Event CreateEvent() =>
        Event.Create(
            "Концерт",
            "Большой зал",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            100);

    private static EventDto CreateEventDto(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Концерт",
        Description = "Большой зал",
        StartAt = DateTime.UtcNow.AddDays(1),
        EndAt = DateTime.UtcNow.AddDays(1).AddHours(2),
        TotalSeats = 100,
        AvailableSeats = 100
    };

    private sealed class EventAvailabilityPublisherStub : IEventAvailabilityPublisher
    {
        public Task PublishAsync(
            EventAvailabilityChanged message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CacheStub : ICacheService
    {
        public Dictionary<string, object> Values { get; } = [];
        public List<string> RemovedKeys { get; } = [];
        public List<string>? Operations { get; init; }
        public TimeSpan? LastTimeToLive { get; private set; }

        public Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(Values.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Values[key] = value;
            LastTimeToLive = timeToLive;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            Operations?.Add("cache");
            RemovedKeys.Add(key);
            Values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class EventRepositoryStub : IEventRepository
    {
        public Event? EventToReturn { get; init; }
        public IReadOnlyList<Event> TopEvents { get; init; } = [];
        public BookingConfirmationResult ConfirmationResult { get; init; }
        public BookingCancellationResult CancellationResult { get; init; }
        public List<string>? Operations { get; init; }
        public int GetByIdCallCount { get; private set; }
        public int GetTopCallCount { get; private set; }

        public Task<Event> AddAsync(
            Event eventItem,
            CancellationToken cancellationToken = default)
        {
            Operations?.Add("database");
            return Task.FromResult(eventItem);
        }

        public Task<IReadOnlyList<Guid>> GetIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<PaginatedResult<Event>> GetAllAsync(
            int page,
            int pageSize,
            string? title,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<Event>
            {
                Page = page,
                PageSize = pageSize,
                Items = []
            });

        public Task<Event?> GetByIdAsync(
            Guid id,
            bool trackChanges = false,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(EventToReturn);
        }

        public Task<IReadOnlyList<Event>> GetTopAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            GetTopCallCount++;
            return Task.FromResult(TopEvents);
        }

        public Task<bool> ExistsAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(EventToReturn is not null);

        public Task<BookingConfirmationResult> ApplyBookingConfirmationAsync(
            Guid bookingId,
            Guid eventId,
            int seats,
            DateTime confirmedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfirmationResult);

        public Task<BookingCancellationResult> ApplyBookingCancellationAsync(
            Guid bookingId,
            Guid eventId,
            int seats,
            DateTime cancelledAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CancellationResult);

        public Task UpdateAsync(
            Event eventItem,
            CancellationToken cancellationToken = default)
        {
            Operations?.Add("database");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            Event eventItem,
            CancellationToken cancellationToken = default)
        {
            Operations?.Add("database");
            return Task.CompletedTask;
        }
    }
}

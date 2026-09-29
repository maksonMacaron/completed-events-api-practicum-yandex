using EventsAPI.Application.Abstractions.Messaging;
using EventsAPI.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace EventsAPI.Infrastructure.Messaging;

public sealed class EventCatalogInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventAvailabilityPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventCatalogInitializer> _logger;

    public EventCatalogInitializer(
        IServiceScopeFactory scopeFactory,
        IEventAvailabilityPublisher publisher,
        TimeProvider timeProvider,
        ILogger<EventCatalogInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var eventIds = await repository.GetIdsAsync(cancellationToken);
            var changedAt = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (var eventId in eventIds)
            {
                await _publisher.PublishAsync(
                    new EventAvailabilityChanged(eventId, true, changedAt),
                    cancellationToken);
            }

            _logger.LogInformation(
                "В каталог бронирований отправлено событий: {EventCount}",
                eventIds.Count);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось синхронизировать каталог событий");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

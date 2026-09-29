using BookingsAPI.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingsAPI.Infrastructure.BackgroundServices;

public sealed class KnownEventCleanupWorker : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KnownEventCleanupWorker> _logger;

    public KnownEventCleanupWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<KnownEventCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var eventCatalog = scope.ServiceProvider.GetRequiredService<IEventCatalog>();
                var threshold = _timeProvider.GetUtcNow().UtcDateTime.Subtract(RetentionPeriod);
                var deletedCount = await eventCatalog.RemoveUnavailableBeforeAsync(
                    threshold,
                    stoppingToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Из локального каталога удалено устаревших событий: {EventCount}",
                        deletedCount);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Не удалось очистить локальный каталог событий");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }
    }
}

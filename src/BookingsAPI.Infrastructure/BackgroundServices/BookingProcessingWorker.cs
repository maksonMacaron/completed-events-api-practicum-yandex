using BookingsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingsAPI.Infrastructure.BackgroundServices;

public sealed class BookingProcessingWorker : BackgroundService
{
    private static readonly TimeSpan MinimumPollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumPollingInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingWorker> _logger;

    public BookingProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = MinimumPollingInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
                processedCount = await service.PreparePendingAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Ошибка фоновой обработки броней");
            }

            pollingInterval = processedCount == 0
                ? IncreaseDelay(pollingInterval)
                : MinimumPollingInterval;
            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    private static TimeSpan IncreaseDelay(TimeSpan current) =>
        TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2, MaximumPollingInterval.TotalSeconds));
}

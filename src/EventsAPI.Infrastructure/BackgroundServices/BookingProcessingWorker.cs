using EventsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsAPI.Infrastructure.BackgroundServices;

/// <summary>Периодически запускает обработку ожидающих бронирований.</summary>
public sealed class BookingProcessingWorker : BackgroundService
{
    private const int DefaultMaxDegreeOfParallelism = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _processingDelay;
    private readonly SemaphoreSlim _processingSlots;

    public BookingProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingWorker> logger)
        : this(
            scopeFactory,
            logger,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            DefaultMaxDegreeOfParallelism)
    {
    }

    public BookingProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingWorker> logger,
        TimeSpan pollInterval,
        TimeSpan processingDelay,
        int maxDegreeOfParallelism = DefaultMaxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = pollInterval;
        _processingDelay = processingDelay;
        _processingSlots = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessingWorker запущен");
        using var timer = new PeriodicTimer(_pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var pendingIds = await GetPendingBookingIdsAsync(stoppingToken);
                    await Task.WhenAll(pendingIds.Select(id =>
                        ProcessBookingWithLimitAsync(id, stoppingToken)));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Не удалось обработать ожидающие бронирования");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("BookingProcessingWorker остановлен");
        }
    }

    public override void Dispose()
    {
        _processingSlots.Dispose();
        base.Dispose();
    }

    private async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
        return await service.GetPendingBookingIdsAsync(cancellationToken);
    }

    private async Task ProcessBookingWithLimitAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        await _processingSlots.WaitAsync(cancellationToken);
        try
        {
            await ProcessBookingAsync(bookingId, cancellationToken);
        }
        finally
        {
            _processingSlots.Release();
        }
    }

    private async Task ProcessBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_processingDelay, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
            await service.ProcessBookingAsync(bookingId, cancellationToken);
            _logger.LogInformation("Бронь {BookingId} обработана", bookingId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await RejectAfterFailureAsync(bookingId, exception, cancellationToken);
        }
    }

    private async Task RejectAfterFailureAsync(
        Guid bookingId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
            await service.RejectAfterFailureAsync(bookingId, cancellationToken);
            _logger.LogError(exception, "Бронь {BookingId} отклонена из-за ошибки", bookingId);
        }
        catch (Exception rejectionException)
        {
            _logger.LogError(rejectionException, "Не удалось отклонить бронь {BookingId}", bookingId);
        }
    }
}

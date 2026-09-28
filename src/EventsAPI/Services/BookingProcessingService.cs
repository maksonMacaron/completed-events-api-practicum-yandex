using EventsAPI.DataAccess.Repositories;
using EventsAPI.Models;

namespace EventsAPI.Services;

/// <summary>Периодически обрабатывает ожидающие бронирования.</summary>
public class BookingProcessingService : BackgroundService
{
    private const int DefaultMaxDegreeOfParallelism = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _processingDelay;
    private readonly SemaphoreSlim _processingSlots;

    public BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingService> logger)
        : this(
            scopeFactory,
            logger,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            DefaultMaxDegreeOfParallelism)
    {
    }

    public BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingService> logger,
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
        _logger.LogInformation("BookingProcessingService запущен");
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось обработать ожидающие бронирования");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("BookingProcessingService остановлен");
        }
    }

    public override void Dispose()
    {
        _processingSlots.Dispose();
        base.Dispose();
    }

    private async Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        return (await bookingRepository.GetPendingIdsAsync(cancellationToken)).ToList();
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

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_processingDelay, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var booking = await bookingRepository.GetByIdAsync(
                bookingId,
                trackChanges: true,
                cancellationToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
                return;

            var eventExists = await eventRepository.ExistsAsync(booking.EventId, cancellationToken);
            if (!eventExists)
            {
                booking.Reject();
                await bookingRepository.UpdateAsync(booking, cancellationToken);
                return;
            }

            booking.Confirm();
            await bookingRepository.UpdateAsync(booking, cancellationToken);
            _logger.LogInformation("Бронь {BookingId} подтверждена", booking.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await RejectAfterFailureAsync(bookingId, ex, cancellationToken);
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
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var booking = await bookingRepository.GetByIdAsync(
                bookingId,
                trackChanges: true,
                cancellationToken);

            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                booking.Reject();
                var eventItem = await eventRepository.GetByIdAsync(
                    booking.EventId,
                    trackChanges: true,
                    cancellationToken);
                eventItem?.ReleaseSeats();
                await bookingRepository.UpdateAsync(booking, cancellationToken);
            }

            _logger.LogError(exception, "Бронь {BookingId} отклонена из-за ошибки", bookingId);
        }
        catch (Exception rejectionException)
        {
            _logger.LogError(rejectionException, "Не удалось отклонить бронь {BookingId}", bookingId);
        }
    }
}

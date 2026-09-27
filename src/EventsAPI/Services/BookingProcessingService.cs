using EventsAPI.DataAccess;
using EventsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Services;

/// <summary>Периодически обрабатывает ожидающие бронирования.</summary>
public class BookingProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _processingDelay;

    public BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingService> logger)
        : this(scopeFactory, logger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
    {
    }

    public BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingService> logger,
        TimeSpan pollInterval,
        TimeSpan processingDelay)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = pollInterval;
        _processingDelay = processingDelay;
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
                    await Task.WhenAll(pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken)));
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

    private async Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Bookings
            .Where(booking => booking.Status == BookingStatus.Pending)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_processingDelay, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings
                .FirstOrDefaultAsync(item => item.Id == bookingId, cancellationToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
                return;

            var eventExists = await context.Events
                .AnyAsync(item => item.Id == booking.EventId, cancellationToken);
            if (!eventExists)
            {
                booking.Reject();
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            booking.Confirm();
            await context.SaveChangesAsync(cancellationToken);
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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings
                .FirstOrDefaultAsync(item => item.Id == bookingId, cancellationToken);

            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                booking.Reject();
                var eventItem = await context.Events
                    .FirstOrDefaultAsync(item => item.Id == booking.EventId, cancellationToken);
                eventItem?.ReleaseSeats();
                await context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogError(exception, "Бронь {BookingId} отклонена из-за ошибки", bookingId);
        }
        catch (Exception rejectionException)
        {
            _logger.LogError(rejectionException, "Не удалось отклонить бронь {BookingId}", bookingId);
        }
    }
}

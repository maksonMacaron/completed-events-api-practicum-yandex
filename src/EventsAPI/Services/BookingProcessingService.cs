using EventsAPI.Models;

namespace EventsAPI.Services;

/// <summary>Периодически обрабатывает ожидающие бронирования.</summary>
public class BookingProcessingService : BackgroundService
{
    private readonly IBookingService _bookingService;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessingService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _processingDelay;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    /// <summary>Создаёт фоновый сервис обработки бронирований.</summary>
    /// <param name="bookingService">Сервис бронирований.</param>
    /// <param name="eventService">Сервис мероприятий.</param>
    /// <param name="logger">Сервис логирования.</param>
    public BookingProcessingService(
        IBookingService bookingService,
        IEventService eventService,
        ILogger<BookingProcessingService> logger)
        : this(bookingService, eventService, logger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
    {
    }

    /// <summary>Создаёт фоновый сервис с заданными интервалами.</summary>
    /// <param name="bookingService">Сервис бронирований.</param>
    /// <param name="eventService">Сервис мероприятий.</param>
    /// <param name="logger">Сервис логирования.</param>
    /// <param name="pollInterval">Интервал опроса ожидающих броней.</param>
    /// <param name="processingDelay">Задержка, имитирующая внешний вызов.</param>
    public BookingProcessingService(
        IBookingService bookingService,
        IEventService eventService,
        ILogger<BookingProcessingService> logger,
        TimeSpan pollInterval,
        TimeSpan processingDelay)
    {
        _bookingService = bookingService;
        _eventService = eventService;
        _logger = logger;
        _pollInterval = pollInterval;
        _processingDelay = processingDelay;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessingService запущен");

        using var timer = new PeriodicTimer(_pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                IReadOnlyList<Booking> pendingBookings;
                try
                {
                    pendingBookings = _bookingService.GetPendingBookings();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось получить ожидающие бронирования");
                    continue;
                }

                var tasks = pendingBookings.Select(booking =>
                    ProcessBookingAsync(booking, stoppingToken));
                await Task.WhenAll(tasks);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("BookingProcessingService остановлен");
        }
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        var semaphoreAcquired = false;

        try
        {
            _logger.LogInformation("Начата обработка брони {BookingId}", booking.Id);
            await Task.Delay(_processingDelay, stoppingToken);

            await _processingSemaphore.WaitAsync(stoppingToken);
            semaphoreAcquired = true;

            try
            {
                _eventService.GetById(booking.EventId);
            }
            catch (KeyNotFoundException)
            {
                _bookingService.RejectBooking(booking.Id);
                _logger.LogWarning(
                    "Бронь {BookingId} отклонена: событие {EventId} удалено",
                    booking.Id,
                    booking.EventId);
                return;
            }

            _bookingService.ConfirmBooking(booking.Id);
            _logger.LogInformation("Бронь {BookingId} подтверждена", booking.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Обработка брони {BookingId} отменена", booking.Id);
        }
        catch (Exception ex)
        {
            try
            {
                _bookingService.RejectBooking(booking.Id);
            }
            catch (Exception rejectionException)
            {
                _logger.LogError(
                    rejectionException,
                    "Не удалось отклонить бронь {BookingId}",
                    booking.Id);
            }

            _logger.LogError(ex, "Не удалось обработать бронь {BookingId}", booking.Id);
        }
        finally
        {
            if (semaphoreAcquired)
                _processingSemaphore.Release();
        }
    }

    public override void Dispose()
    {
        _processingSemaphore.Dispose();
        base.Dispose();
    }
}

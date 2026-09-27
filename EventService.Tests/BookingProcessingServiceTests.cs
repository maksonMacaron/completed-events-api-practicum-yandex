using System.Diagnostics;
using EventsAPI.Models;
using EventsAPI.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventsAPI.Tests;

public class BookingProcessingServiceTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task BackgroundService_ConfirmsPendingBookingAfterDelay()
    {
        // Arrange
        var eventService = new EventService(new List<Event>());
        var eventModel = eventService.Create(NewEvent());
        var bookingService = new BookingService(eventService);
        var booking = await bookingService.CreateBookingAsync(eventModel.Id);
        using var worker = CreateWorker(bookingService, eventService, ShortInterval);

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => booking.Status != BookingStatus.Pending);

            // Assert
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
            Assert.True(booking.ProcessedAt >= booking.CreatedAt);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_EventDeleted_RejectsPendingBooking()
    {
        var eventService = new EventService([]);
        var eventModel = eventService.Create(NewEvent());
        var bookingService = new BookingService(eventService);
        var booking = await bookingService.CreateBookingAsync(eventModel.Id);
        eventService.Delete(eventModel.Id);
        using var worker = CreateWorker(bookingService, eventService, ShortInterval);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => booking.Status != BookingStatus.Pending);

            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_Cancellation_StopsDuringProcessingDelay()
    {
        // Arrange
        var eventService = new EventService([]);
        var eventModel = eventService.Create(NewEvent());
        var fakeService = new FakeBookingService(new Booking(eventModel.Id));
        using var worker = CreateWorker(fakeService, eventService, TimeSpan.FromMinutes(1));
        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => fakeService.GetPendingCalls > 0);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(0, fakeService.ConfirmCalls);
    }

    [Fact]
    public async Task BackgroundService_GetPendingThrows_ContinuesPolling()
    {
        // Arrange
        var fakeService = new FakeBookingService { GetPendingFailuresRemaining = 1 };
        using var worker = CreateWorker(fakeService, new EventService([]), ShortInterval);

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => fakeService.GetPendingCalls >= 2);

            // Assert
            Assert.True(fakeService.GetPendingCalls >= 2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_ConfirmThrows_ContinuesRunning()
    {
        // Arrange
        var eventService = new EventService([]);
        var eventModel = eventService.Create(NewEvent());
        var fakeService = new FakeBookingService(new Booking(eventModel.Id))
        {
            ThrowOnConfirm = true
        };
        using var worker = CreateWorker(fakeService, eventService, ShortInterval);

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => fakeService.ConfirmCalls >= 2);

            // Assert
            Assert.True(fakeService.ConfirmCalls >= 2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private static BookingProcessingService CreateWorker(
        IBookingService bookingService,
        IEventService eventService,
        TimeSpan processingDelay) =>
        new(
            bookingService,
            eventService,
            NullLogger<BookingProcessingService>.Instance,
            ShortInterval,
            processingDelay);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static Event NewEvent() =>
        new("Тестовое событие", null, new DateTime(2026, 10, 1), new DateTime(2026, 10, 2), 100);

    private sealed class FakeBookingService : IBookingService
    {
        private readonly IReadOnlyList<Booking> _pendingBookings;
        private int _getPendingCalls;
        private int _confirmCalls;

        public FakeBookingService(params Booking[] pendingBookings)
        {
            _pendingBookings = pendingBookings;
        }

        public int GetPendingCalls => Volatile.Read(ref _getPendingCalls);

        public int ConfirmCalls => Volatile.Read(ref _confirmCalls);

        public int GetPendingFailuresRemaining { get; set; }

        public bool ThrowOnConfirm { get; init; }

        public Task<Booking> CreateBookingAsync(Guid eventId) => throw new NotSupportedException();

        public Task<Booking> GetBookingByIdAsync(Guid bookingId) => throw new NotSupportedException();

        public IReadOnlyList<Booking> GetPendingBookings()
        {
            Interlocked.Increment(ref _getPendingCalls);
            if (GetPendingFailuresRemaining > 0)
            {
                GetPendingFailuresRemaining--;
                throw new InvalidOperationException("Ошибка чтения");
            }

            return _pendingBookings;
        }

        public void ConfirmBooking(Guid bookingId)
        {
            Interlocked.Increment(ref _confirmCalls);
            if (ThrowOnConfirm)
                throw new InvalidOperationException("Ошибка подтверждения");
        }

        public void RejectBooking(Guid bookingId) => throw new NotSupportedException();
    }
}

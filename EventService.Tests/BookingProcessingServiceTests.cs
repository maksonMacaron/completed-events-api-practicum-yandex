using System.Diagnostics;
using EventsAPI.Application.DTOs;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using EventsAPI.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventsAPI.Tests;

public sealed class BookingProcessingServiceTests : IDisposable
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(10);
    private readonly ServiceProvider _provider = TestServices.BuildProvider();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task BackgroundService_ConfirmsPendingBookingAfterDelay()
    {
        var bookingId = await CreatePendingBookingAsync();
        using var worker = CreateWorker(ShortInterval);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var booking = await WaitForBookingStatusAsync(bookingId, BookingStatus.Confirmed);

            Assert.NotNull(booking.ProcessedAt);
            Assert.True(booking.ProcessedAt >= booking.CreatedAt);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_ProcessesMultipleBookings()
    {
        var firstId = await CreatePendingBookingAsync();
        var secondId = await CreatePendingBookingAsync();
        using var worker = CreateWorker(ShortInterval);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForBookingStatusAsync(firstId, BookingStatus.Confirmed);
            await WaitForBookingStatusAsync(secondId, BookingStatus.Confirmed);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_Cancellation_StopsDuringProcessingDelay()
    {
        var bookingId = await CreatePendingBookingAsync();
        using var worker = CreateWorker(TimeSpan.FromMinutes(1));
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        var stopwatch = Stopwatch.StartNew();

        await worker.StopAsync(CancellationToken.None);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        Assert.Equal(BookingStatus.Pending, (await service.GetBookingByIdAsync(bookingId)).Status);
    }

    [Fact]
    public async Task ProcessBookingAsync_BookingCancelledAfterRead_DoesNotConfirmStaleCopy()
    {
        var bookingId = await CreatePendingBookingAsync();
        Booking staleBooking;
        using (var readScope = _provider.CreateScope())
        {
            var processingService = readScope.ServiceProvider
                .GetRequiredService<IBookingProcessingService>();
            staleBooking = Assert.Single(await processingService.GetPendingBookingsAsync());
        }

        using (var cancellationScope = _provider.CreateScope())
        {
            var bookingService = cancellationScope.ServiceProvider
                .GetRequiredService<IBookingService>();
            await bookingService.CancelBookingAsync(
                bookingId,
                TestServices.UserId,
                isAdmin: false);
        }

        using (var processingScope = _provider.CreateScope())
        {
            var processingService = processingScope.ServiceProvider
                .GetRequiredService<IBookingProcessingService>();
            await processingService.ProcessBookingAsync(staleBooking);
        }

        using var verificationScope = _provider.CreateScope();
        var verificationBookingService = verificationScope.ServiceProvider
            .GetRequiredService<IBookingService>();
        var verificationEventService = verificationScope.ServiceProvider
            .GetRequiredService<IEventService>();
        var booking = await verificationBookingService.GetBookingByIdAsync(bookingId);
        var eventItem = await verificationEventService.GetByIdAsync(staleBooking.EventId);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(eventItem.TotalSeats, eventItem.AvailableSeats);
    }

    private BookingProcessingWorker CreateWorker(TimeSpan processingDelay) => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<BookingProcessingWorker>.Instance,
        ShortInterval,
        processingDelay);

    private async Task<Guid> CreatePendingBookingAsync()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent($"Событие {Guid.NewGuid()}"));
        return (await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId)).Id;
    }

    private async Task<BookingDto> WaitForBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            using var scope = _provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var booking = await service.GetBookingByIdAsync(bookingId, timeout.Token);
            if (booking.Status == status)
                return booking;

            await Task.Delay(10, timeout.Token);
        }
    }
}

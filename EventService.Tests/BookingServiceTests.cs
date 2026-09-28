using System.Collections.Concurrent;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using EventsAPI.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Tests;

public sealed class BookingServiceTests : IDisposable
{
    private readonly ServiceProvider _provider = TestServices.BuildProvider();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_CreatesPendingBooking()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Концерт"));
        var booking = await bookingService.CreateBookingAsync(eventInfo.Id);
        var updatedEvent = await eventService.GetByIdAsync(eventInfo.Id);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventInfo.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(TestServices.UtcNow.UtcDateTime, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);
        Assert.Equal(99, updatedEvent.AvailableSeats);

    }

    [Fact]
    public async Task CreateBookingAsync_MissingEvent_Throws()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await Assert.ThrowsAsync<EventNotFoundException>(() =>
            service.CreateBookingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_NoAvailableSeats_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Камерный концерт", seats: 1));
        await service.CreateBookingAsync(eventInfo.Id);

        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(() =>
            service.CreateBookingAsync(eventInfo.Id));

        Assert.Equal("No available seats for this event", exception.Message);
    }

    [Fact]
    public async Task ConfirmAndRejectBookingAsync_UpdateStatusesAndSeats()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Конференция", seats: 2));
        var confirmed = await service.CreateBookingAsync(eventInfo.Id);
        var rejected = await service.CreateBookingAsync(eventInfo.Id);

        await service.ConfirmBookingAsync(confirmed.Id);
        await service.RejectBookingAsync(rejected.Id);
        var confirmedResult = await service.GetBookingByIdAsync(confirmed.Id);
        var rejectedResult = await service.GetBookingByIdAsync(rejected.Id);
        var updatedEvent = await eventService.GetByIdAsync(eventInfo.Id);

        Assert.Equal(BookingStatus.Confirmed, confirmedResult.Status);
        Assert.Equal(TestServices.UtcNow.UtcDateTime, confirmedResult.ProcessedAt);
        Assert.Equal(BookingStatus.Rejected, rejectedResult.Status);
        Assert.Equal(TestServices.UtcNow.UtcDateTime, rejectedResult.ProcessedAt);
        Assert.Equal(1, updatedEvent.AvailableSeats);
        Assert.Empty(await service.GetPendingBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentScopes_PreventOverbooking()
    {
        const int totalSeats = 5;
        const int requestCount = 20;
        Guid eventId;

        using (var scope = _provider.CreateScope())
        {
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
            eventId = (await eventService.CreateEventAsync(
                EventServiceTests.NewCreateEvent("Популярное событие", seats: totalSeats))).Id;
        }

        var bookingIds = new ConcurrentBag<Guid>();
        var tasks = Enumerable.Range(0, requestCount).Select(_ => Task.Run(async () =>
        {
            using var scope = _provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
            try
            {
                var booking = await service.CreateBookingAsync(eventId);
                bookingIds.Add(booking.Id);
                return true;
            }
            catch (NoAvailableSeatsException)
            {
                return false;
            }
        }));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(totalSeats, results.Count(success => success));
        Assert.Equal(totalSeats, bookingIds.Distinct().Count());
        using var verificationScope = _provider.CreateScope();
        var verificationService = verificationScope.ServiceProvider.GetRequiredService<IEventService>();
        Assert.Equal(0, (await verificationService.GetByIdAsync(eventId)).AvailableSeats);
    }
}

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
        var booking = await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);
        var updatedEvent = await eventService.GetByIdAsync(eventInfo.Id);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventInfo.Id, booking.EventId);
        Assert.Equal(TestServices.UserId, booking.UserId);
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
            service.CreateBookingAsync(Guid.NewGuid(), TestServices.UserId));
    }

    [Fact]
    public async Task CreateBookingAsync_NoAvailableSeats_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Камерный концерт", seats: 1));
        await service.CreateBookingAsync(eventInfo.Id, TestServices.UserId);

        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(() =>
            service.CreateBookingAsync(eventInfo.Id, TestServices.UserId));

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
        var confirmed = await service.CreateBookingAsync(eventInfo.Id, TestServices.UserId);
        var rejected = await service.CreateBookingAsync(eventInfo.Id, TestServices.UserId);

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
                var booking = await service.CreateBookingAsync(eventId, TestServices.UserId);
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

    [Fact]
    public async Task CreateBookingAsync_StartedEvent_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var startAt = TestServices.UtcNow.UtcDateTime.AddHours(-2);
        var eventInfo = await eventService.CreateEventAsync(new EventsAPI.Application.DTOs.CreateEvent
        {
            Title = "Завершившаяся лекция",
            StartAt = startAt,
            EndAt = startAt.AddHours(1),
            TotalSeats = 10
        });

        await Assert.ThrowsAsync<PastEventBookingException>(() =>
            bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId));
    }

    [Fact]
    public async Task CreateBookingAsync_ActiveBookingLimitReached_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var index = 0; index < BookingService.ActiveBookingLimit; index++)
        {
            var eventInfo = await eventService.CreateEventAsync(
                EventServiceTests.NewCreateEvent($"Событие {index}"));
            await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);
        }

        var nextEvent = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Лишнее событие"));
        var exception = await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(() =>
            bookingService.CreateBookingAsync(nextEvent.Id, TestServices.UserId));

        Assert.Contains(BookingService.ActiveBookingLimit.ToString(), exception.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_DifferentUsers_HaveIndependentLimits()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var index = 0; index < BookingService.ActiveBookingLimit; index++)
        {
            var eventInfo = await eventService.CreateEventAsync(
                EventServiceTests.NewCreateEvent($"Первый пользователь {index}", seats: 2));
            await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);
            await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.OtherUserId);
        }

        var firstUserEvent = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Лимит первого пользователя"));
        await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(() =>
            bookingService.CreateBookingAsync(firstUserEvent.Id, TestServices.UserId));

        var thirdUserId = Guid.NewGuid();
        var thirdUserBooking = await bookingService.CreateBookingAsync(
            firstUserEvent.Id,
            thirdUserId);
        Assert.Equal(thirdUserId, thirdUserBooking.UserId);
    }

    [Fact]
    public async Task CancelBookingAsync_OtherUserWithoutAdminRole_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Закрытое мероприятие"));
        var booking = await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            bookingService.CancelBookingAsync(
                booking.Id,
                TestServices.OtherUserId,
                isAdmin: false));
    }

    [Fact]
    public async Task CancelBookingAsync_AdminCancelsOtherUsersBooking()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Отмена администратором", seats: 1));
        var booking = await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);

        await bookingService.CancelBookingAsync(
            booking.Id,
            TestServices.OtherUserId,
            isAdmin: true);

        var cancelled = await bookingService.GetBookingByIdAsync(booking.Id);
        var updatedEvent = await eventService.GetByIdAsync(eventInfo.Id);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(1, updatedEvent.AvailableSeats);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelledBooking_Throws()
    {
        using var scope = _provider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var eventInfo = await eventService.CreateEventAsync(
            EventServiceTests.NewCreateEvent("Повторная отмена"));
        var booking = await bookingService.CreateBookingAsync(eventInfo.Id, TestServices.UserId);
        await bookingService.CancelBookingAsync(
            booking.Id,
            TestServices.UserId,
            isAdmin: false);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            bookingService.CancelBookingAsync(
                booking.Id,
                TestServices.UserId,
                isAdmin: false));
    }
}

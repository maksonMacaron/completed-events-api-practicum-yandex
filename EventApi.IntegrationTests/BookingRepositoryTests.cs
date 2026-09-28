using EventsAPI.Domain.Entities;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests
{
    private static readonly TimeProvider Clock = TimeProvider.System;
    private readonly PostgreSqlFixture _fixture;

    public BookingRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_SavesBooking()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent();
        var user = CreateUser("booking-owner");
        var booking = new Booking(eventItem.Id, user.Id, Clock);
        await using (var arrangeContext = _fixture.CreateContext())
        {
            await new EventRepository(arrangeContext).AddAsync(eventItem);
            await new UserRepository(arrangeContext).AddAsync(user);
        }

        await using (var context = _fixture.CreateContext())
        {
            var repository = new BookingRepository(context);

            // Act
            await repository.AddAsync(booking);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var saved = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.NotNull(saved);
        Assert.Equal(eventItem.Id, saved.EventId);
        Assert.Equal(BookingStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task AddAsync_Throws_WhenEventDoesNotExist()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateContext();
        var repository = new BookingRepository(context);
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), Clock);

        // Act
        var action = () => repository.AddAsync(booking);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(action);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenBookingDoesNotExist()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateContext();
        var repository = new BookingRepository(context);

        // Act
        var booking = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(booking);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBooking()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var booking = await CreateBookingAsync();

        await using (var context = _fixture.CreateContext())
        {
            var repository = new BookingRepository(context);
            var saved = await repository.GetByIdAsync(booking.Id, trackChanges: true);
            Assert.NotNull(saved);
            saved.Confirm(Clock);

            // Act
            await repository.UpdateAsync(saved);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var updated = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.NotNull(updated);
        Assert.Equal(BookingStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task TryConfirmPendingAsync_CancelledBooking_DoesNotChangeStatus()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var booking = await CreateBookingAsync();
        await using (var cancellationContext = _fixture.CreateContext())
        {
            var repository = new BookingRepository(cancellationContext);
            var saved = await repository.GetByIdAsync(booking.Id, trackChanges: true);
            Assert.NotNull(saved);
            saved.Cancel(Clock);
            await repository.UpdateAsync(saved);
        }

        await using (var processingContext = _fixture.CreateContext())
        {
            var repository = new BookingRepository(processingContext);

            // Act
            var confirmed = await repository.TryConfirmPendingAsync(
                booking.Id,
                DateTime.UtcNow);

            // Assert
            Assert.False(confirmed);
        }

        await using var assertContext = _fixture.CreateContext();
        var result = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_DeletesBooking()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var booking = await CreateBookingAsync();

        await using (var context = _fixture.CreateContext())
        {
            var repository = new BookingRepository(context);
            var saved = await repository.GetByIdAsync(booking.Id, trackChanges: true);
            Assert.NotNull(saved);

            // Act
            await repository.DeleteAsync(saved);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        Assert.Null(await new BookingRepository(assertContext).GetByIdAsync(booking.Id));
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent();
        var user = CreateUser("pending-owner");
        var first = new Booking(eventItem.Id, user.Id, Clock);
        var second = new Booking(eventItem.Id, user.Id, Clock);
        var confirmed = new Booking(eventItem.Id, user.Id, Clock);
        confirmed.Confirm(Clock);

        await using (var arrangeContext = _fixture.CreateContext())
        {
            await new EventRepository(arrangeContext).AddAsync(eventItem);
            await new UserRepository(arrangeContext).AddAsync(user);
            var repository = new BookingRepository(arrangeContext);
            await repository.AddAsync(first);
            await repository.AddAsync(second);
            await repository.AddAsync(confirmed);
        }

        await using var context = _fixture.CreateContext();
        var bookingRepository = new BookingRepository(context);

        // Act
        var pending = await bookingRepository.GetPendingAsync();
        var pendingWithEvents = await bookingRepository.GetPendingWithEventsAsync();

        // Assert
        var expectedIds = new[] { first.Id, second.Id }.OrderBy(id => id);
        Assert.Equal(expectedIds, pending.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(expectedIds, pendingWithEvents.Select(item => item.Id).OrderBy(id => id));
        Assert.All(pendingWithEvents, item => Assert.Equal(eventItem.Id, item.Event.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private async Task<Booking> CreateBookingAsync()
    {
        var eventItem = CreateEvent();
        var user = CreateUser($"owner-{Guid.NewGuid():N}");
        var booking = new Booking(eventItem.Id, user.Id, Clock);

        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(eventItem);
        await new UserRepository(context).AddAsync(user);
        await new BookingRepository(context).AddAsync(booking);
        return booking;
    }

    private static Event CreateEvent()
    {
        var start = new DateTime(2027, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        return Event.Create("Тестовое событие", null, start, start.AddHours(2), 10);
    }

    private static User CreateUser(string login) =>
        new(login, new string('A', 64), UserRole.User);
}

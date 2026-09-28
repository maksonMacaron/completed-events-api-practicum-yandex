using EventsAPI.DataAccess.Repositories;
using EventsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests
{
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
        var booking = new Booking(eventItem.Id);
        await using (var arrangeContext = _fixture.CreateContext())
            await new EventRepository(arrangeContext).AddAsync(eventItem);

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
        var booking = new Booking(Guid.NewGuid());

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
            saved.Confirm();

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
        var first = new Booking(eventItem.Id);
        var second = new Booking(eventItem.Id);
        var confirmed = new Booking(eventItem.Id);
        confirmed.Confirm();

        await using (var arrangeContext = _fixture.CreateContext())
        {
            await new EventRepository(arrangeContext).AddAsync(eventItem);
            var repository = new BookingRepository(arrangeContext);
            await repository.AddAsync(first);
            await repository.AddAsync(second);
            await repository.AddAsync(confirmed);
        }

        await using var context = _fixture.CreateContext();
        var bookingRepository = new BookingRepository(context);

        // Act
        var pending = await bookingRepository.GetPendingAsync();
        var pendingIds = await bookingRepository.GetPendingIdsAsync();

        // Assert
        var expectedIds = new[] { first.Id, second.Id }.OrderBy(id => id);
        Assert.Equal(expectedIds, pending.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(expectedIds, pendingIds.OrderBy(id => id));
    }

    private async Task<Booking> CreateBookingAsync()
    {
        var eventItem = CreateEvent();
        var booking = new Booking(eventItem.Id);

        await using var context = _fixture.CreateContext();
        await new EventRepository(context).AddAsync(eventItem);
        await new BookingRepository(context).AddAsync(booking);
        return booking;
    }

    private static Event CreateEvent()
    {
        var start = new DateTime(2027, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        return Event.Create("Тестовое событие", null, start, start.AddHours(2), 10);
    }
}

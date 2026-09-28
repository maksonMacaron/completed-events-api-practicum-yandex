using EventsAPI.Domain.Entities;
using EventsAPI.Infrastructure.Persistence.Repositories;

namespace EventsAPI.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;

    public EventRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_SavesEvent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent("Концерт", 10);
        await using (var context = _fixture.CreateContext())
        {
            var repository = new EventRepository(context);

            // Act
            await repository.AddAsync(eventItem);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var saved = await new EventRepository(assertContext).GetByIdAsync(eventItem.Id);
        Assert.NotNull(saved);
        Assert.Equal(eventItem.Title, saved.Title);
        Assert.Equal(eventItem.TotalSeats, saved.TotalSeats);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenEventDoesNotExist()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);

        // Act
        var eventItem = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(eventItem);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsExpectedResult()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent("Лекция", 20);
        await using (var arrangeContext = _fixture.CreateContext())
            await new EventRepository(arrangeContext).AddAsync(eventItem);

        await using var context = _fixture.CreateContext();
        var repository = new EventRepository(context);

        // Act
        var existingEventFound = await repository.ExistsAsync(eventItem.Id);
        var missingEventFound = await repository.ExistsAsync(Guid.NewGuid());

        // Assert
        Assert.True(existingEventFound);
        Assert.False(missingEventFound);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEvent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent("Старое название", 15);
        await using (var arrangeContext = _fixture.CreateContext())
            await new EventRepository(arrangeContext).AddAsync(eventItem);

        await using (var context = _fixture.CreateContext())
        {
            var repository = new EventRepository(context);
            var saved = await repository.GetByIdAsync(eventItem.Id, trackChanges: true);
            Assert.NotNull(saved);
            saved.UpdateDetails(
                "Новое название",
                saved.Description,
                saved.StartAt,
                saved.EndAt);

            // Act
            await repository.UpdateAsync(saved);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var updated = await new EventRepository(assertContext).GetByIdAsync(eventItem.Id);
        Assert.NotNull(updated);
        Assert.Equal("Новое название", updated.Title);
    }

    [Fact]
    public async Task DeleteAsync_DeletesEventAndRelatedBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var eventItem = CreateEvent("Выставка", 5);
        var booking = new Booking(eventItem.Id);
        await using (var arrangeContext = _fixture.CreateContext())
        {
            await new EventRepository(arrangeContext).AddAsync(eventItem);
            await new BookingRepository(arrangeContext).AddAsync(booking);
        }

        await using (var context = _fixture.CreateContext())
        {
            var repository = new EventRepository(context);
            var saved = await repository.GetByIdAsync(eventItem.Id, trackChanges: true);
            Assert.NotNull(saved);

            // Act
            await repository.DeleteAsync(saved);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        Assert.Null(await new EventRepository(assertContext).GetByIdAsync(eventItem.Id));
        Assert.Null(await new BookingRepository(assertContext).GetByIdAsync(booking.Id));
    }

    [Fact]
    public async Task GetAllAsync_AppliesFiltersSortingAndPagination()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var start = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var events = new[]
        {
            CreateEvent("Концерт камерной музыки", 10, start, start.AddHours(2)),
            CreateEvent("Театральная премьера", 20, start.AddDays(1), start.AddDays(1).AddHours(3)),
            CreateEvent("РОК-КОНЦЕРТ", 30, start.AddDays(2), start.AddDays(2).AddHours(2)),
            CreateEvent("Выставка", 40, start.AddDays(3), start.AddDays(3).AddHours(4))
        };

        await using (var arrangeContext = _fixture.CreateContext())
        {
            var repository = new EventRepository(arrangeContext);
            foreach (var eventItem in events)
                await repository.AddAsync(eventItem);
        }

        await using var context = _fixture.CreateContext();
        var eventRepository = new EventRepository(context);

        // Act
        var all = await eventRepository.GetAllAsync(1, 10, null, null, null);
        var byTitle = await eventRepository.GetAllAsync(1, 10, "концерт", null, null);
        var byStart = await eventRepository.GetAllAsync(1, 10, null, start.AddDays(2), null);
        var byEnd = await eventRepository.GetAllAsync(1, 10, null, null, start.AddDays(1).AddHours(3));
        var combined = await eventRepository.GetAllAsync(
            1,
            10,
            "концерт",
            start.AddDays(1),
            start.AddDays(3));
        var secondPage = await eventRepository.GetAllAsync(2, 2, null, null, null);

        // Assert
        Assert.Equal(4, all.Total);
        Assert.Equal(events.Select(item => item.Id), all.Items.Select(item => item.Id));
        Assert.Equal(2, byTitle.Total);
        Assert.Equal(2, byStart.Total);
        Assert.Equal(2, byEnd.Total);
        Assert.Single(combined.Items);
        Assert.Equal(events[2].Id, combined.Items.Single().Id);
        Assert.Equal(4, secondPage.Total);
        Assert.Equal(2, secondPage.Count);
        Assert.Equal(new[] { events[2].Id, events[3].Id }, secondPage.Items.Select(item => item.Id));
    }

    private static Event CreateEvent(
        string title,
        int totalSeats,
        DateTime? startAt = null,
        DateTime? endAt = null)
    {
        var start = startAt ?? new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        return Event.Create(title, null, start, endAt ?? start.AddHours(2), totalSeats);
    }
}

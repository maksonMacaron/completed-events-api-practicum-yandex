using EventsAPI.Models;
using EventsAPI.Services;

namespace EventsAPI.Tests;

public class EventServiceTests
{
    private static List<Event> CreateTestEvents()
    {
        return new List<Event>
        {
            new Event("Концерт", "Музыкальное событие", new DateTime(2026, 5, 1), new DateTime(2026, 5, 2), 100),
            new Event("Спектакль", "Театр", new DateTime(2026, 5, 10), new DateTime(2026, 5, 11), 100),
            new Event("Конференция C#", "IT", new DateTime(2026, 6, 1), new DateTime(2026, 6, 2), 100),
            new Event("Концерт группы", null, new DateTime(2026, 6, 10), new DateTime(2026, 6, 11), 100),
        };
    }

    private static IEventService CreateService()
    {
        return new EventService(CreateTestEvents());
    }

    [Fact]
    public void Create_ShouldAddAndReturnCreatedEvent()
    {
        // Arrange
        var service = CreateService();

        var expectedEvent = new Event(
            "Тестовое мероприятие",
            "Описание",
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 2),
            100);

        // Act
        var createdEvent = service.Create(expectedEvent);

        // Assert
        Assert.NotNull(createdEvent);
        Assert.NotEqual(Guid.Empty, createdEvent.Id);
        Assert.Equal(expectedEvent.Title, createdEvent.Title);
        Assert.Equal(expectedEvent.Description, createdEvent.Description);
        Assert.Equal(expectedEvent.StartAt, createdEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, createdEvent.EndAt);

        var savedEvent = service.GetById(createdEvent.Id);
        Assert.Equal(createdEvent.Id, savedEvent.Id);
    }

    [Fact]
    public void GetAll_ShouldReturnAllEvents()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAll(1, 10, null, null, null);

        // Assert
        Assert.Equal(4, result.Total);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void GetById_WhenEventExists_ShouldReturnEvent()
    {
        // Arrange
        var events = CreateTestEvents();
        var service = new EventService(events);

        var expectedEvent = events[0];

        // Act
        var result = service.GetById(expectedEvent.Id);

        // Assert
        Assert.Equal(expectedEvent.Id, result.Id);
        Assert.Equal(expectedEvent.Title, result.Title);
    }

    [Fact]
    public void Update_WhenEventExists_ShouldUpdateEvent()
    {
        // Arrange
        var events = CreateTestEvents();
        var service = new EventService(events);

        var expectedEvent = events[0];

        var updateModel = new Event(
            "Обновлённое мероприятие",
            "Новое описание",
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 2),
            100);

        // Act
        var updatedEvent = service.Update(expectedEvent.Id, updateModel);

        // Assert
        Assert.Equal(expectedEvent.Id, updatedEvent.Id);
        Assert.Equal(updateModel.Title, updatedEvent.Title);
        Assert.Equal(updateModel.Description, updatedEvent.Description);
        Assert.Equal(updateModel.StartAt, updatedEvent.StartAt);
        Assert.Equal(updateModel.EndAt, updatedEvent.EndAt);
    }

    [Fact]
    public void Delete_WhenEventExists_ShouldRemoveEvent()
    {
        // Arrange
        var events = CreateTestEvents();
        var service = new EventService(events);

        var expectedEvent = events[0];

        // Act
        service.Delete(expectedEvent.Id);

        // Assert
        Assert.Throws<KeyNotFoundException>(() => service.GetById(expectedEvent.Id));
    }

    [Fact]
    public void GetAll_WhenTitleFilterProvided_ShouldReturnMatchingEvents()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAll(1, 10, "концерт", null, null);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item =>
            Assert.Contains("концерт", item.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAll_WhenDateFiltersProvided_ShouldReturnEventsInRange()
    {
        // Arrange
        var service = CreateService();

        var from = new DateTime(2026, 6, 1);
        var to = new DateTime(2026, 6, 30);

        // Act
        var result = service.GetAll(1, 10, null, from, to);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item =>
        {
            Assert.True(item.StartAt >= from);
            Assert.True(item.EndAt <= to);
        });
    }

    [Fact]
    public void GetAll_WhenPaginationProvided_ShouldReturnCorrectPage()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetAll(2, 2, null, null, null);

        // Assert
        Assert.Equal(4, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetAll_WhenCombinedFiltersProvided_ShouldReturnMatchingEvents()
    {
        // Arrange
        var service = CreateService();

        var from = new DateTime(2026, 6, 1);
        var to = new DateTime(2026, 6, 30);

        // Act
        var result = service.GetAll(1, 10, "концерт", from, to);

        // Assert
        Assert.Equal(1, result.Total);

        var expectedEvent = result.Items.Single();

        Assert.Contains("концерт", expectedEvent.Title, StringComparison.OrdinalIgnoreCase);
        Assert.True(expectedEvent.StartAt >= from);
        Assert.True(expectedEvent.EndAt <= to);
    }

    [Fact]
    public void GetById_WhenEventDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var service = CreateService();
        var notExpectedEventId = Guid.NewGuid();

        // Act + Assert
        Assert.Throws<KeyNotFoundException>(() => service.GetById(notExpectedEventId));
    }

    [Fact]
    public void Update_WhenEventDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var service = CreateService();
        var notExpectedEventId = Guid.NewGuid();

        var updateModel = new Event(
            "Несуществующее мероприятие",
            "Описание",
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 2),
            100);

        // Act + Assert
        Assert.Throws<KeyNotFoundException>(() =>
            service.Update(notExpectedEventId, updateModel));
    }
}

using EventsAPI.DTOs;
using EventsAPI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly ServiceProvider _provider = TestServices.BuildProvider();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task CreateEventAsync_AddsAndReturnsEvent()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var request = NewCreateEvent("Тестовое мероприятие");

        var created = await service.CreateEventAsync(request);
        var saved = await service.GetByIdAsync(created.Id);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(request.TotalSeats, saved.TotalSeats);
        Assert.Equal(request.TotalSeats, saved.AvailableSeats);
    }

    [Fact]
    public async Task GetAllAsync_AppliesDateFiltersAndPagination()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var first = NewCreateEvent("Концерт группы", 1);
        var second = NewCreateEvent("Спектакль", 5);
        await service.CreateEventAsync(first);
        await service.CreateEventAsync(second);

        var result = await service.GetAllAsync(
            page: 1,
            pageSize: 10,
            title: null,
            from: first.StartAt.AddHours(-1),
            to: first.EndAt.AddHours(1));

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Count);
        Assert.Equal("Концерт группы", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesStoredEvent()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var created = await service.CreateEventAsync(NewCreateEvent("Исходное название"));
        var startAt = DateTime.UtcNow.AddDays(10);

        var updated = await service.UpdateAsync(created.Id, new EventDto
        {
            Id = created.Id,
            Title = "Новое название",
            Description = "Новое описание",
            StartAt = startAt,
            EndAt = startAt.AddHours(2)
        });

        Assert.Equal("Новое название", updated.Title);
        Assert.Equal("Новое описание", updated.Description);
        Assert.Equal(startAt, updated.StartAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEvent()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var created = await service.CreateEventAsync(NewCreateEvent("Для удаления"));

        await service.DeleteAsync(created.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task GetByIdAsync_MissingEvent_Throws()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid()));
    }

    internal static CreateEvent NewCreateEvent(string title, int daysFromNow = 1, int seats = 100)
    {
        var startAt = DateTime.UtcNow.AddDays(daysFromNow);
        return new CreateEvent
        {
            Title = title,
            Description = "Описание",
            StartAt = startAt,
            EndAt = startAt.AddHours(2),
            TotalSeats = seats
        };
    }
}

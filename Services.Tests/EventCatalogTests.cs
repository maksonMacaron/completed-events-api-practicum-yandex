using BookingsAPI.Infrastructure.Persistence;
using BookingsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Services.Tests;

public sealed class EventCatalogTests
{
    [Fact]
    public async Task ApplyAsync_KeepsLatestAvailabilityState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new BookingsDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var catalog = new EventCatalog(context);
        var eventId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        await catalog.ApplyAsync(eventId, isAvailable: true, createdAt);
        await catalog.ApplyAsync(eventId, isAvailable: false, createdAt.AddMinutes(-1));

        Assert.True(await catalog.ExistsAsync(eventId));

        await catalog.ApplyAsync(eventId, isAvailable: false, createdAt.AddMinutes(1));

        Assert.False(await catalog.ExistsAsync(eventId));

        var deletedCount = await catalog.RemoveUnavailableBeforeAsync(
            createdAt.AddMinutes(2));

        Assert.Equal(1, deletedCount);
        Assert.Empty(await context.KnownEvents.ToListAsync());
    }
}

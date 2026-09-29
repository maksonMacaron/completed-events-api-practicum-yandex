using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAPI.Infrastructure.Persistence.Repositories;

public sealed class EventCatalog : IEventCatalog
{
    private readonly BookingsDbContext _context;

    public EventCatalog(BookingsDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsAsync(
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        _context.KnownEvents.AnyAsync(
            item => item.EventId == eventId && item.IsAvailable,
            cancellationToken);

    public async Task ApplyAsync(
        Guid eventId,
        bool isAvailable,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO known_events (event_id, is_available, updated_at)
                VALUES ({{eventId}}, {{isAvailable}}, {{changedAt}})
                ON CONFLICT (event_id) DO UPDATE
                SET is_available = EXCLUDED.is_available,
                    updated_at = EXCLUDED.updated_at
                WHERE EXCLUDED.updated_at >= known_events.updated_at
                """, cancellationToken);
            return;
        }

        var eventItem = await _context.KnownEvents.FirstOrDefaultAsync(
            item => item.EventId == eventId,
            cancellationToken);

        if (eventItem is null)
        {
            _context.KnownEvents.Add(new KnownEvent(eventId, isAvailable, changedAt));
        }
        else
        {
            eventItem.Apply(isAvailable, changedAt);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> RemoveUnavailableBeforeAsync(
        DateTime threshold,
        CancellationToken cancellationToken = default) =>
        _context.KnownEvents
            .Where(item => !item.IsAvailable && item.UpdatedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);
}

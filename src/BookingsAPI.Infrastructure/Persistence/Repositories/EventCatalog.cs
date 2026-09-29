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
}

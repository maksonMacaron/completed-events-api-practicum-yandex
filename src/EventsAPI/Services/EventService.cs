using System.ComponentModel.DataAnnotations;
using EventsAPI.DataAccess;
using EventsAPI.DTOs;
using EventsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EventInfo> CreateEventAsync(
        CreateEvent item,
        CancellationToken cancellationToken = default)
    {
        if (item.TotalSeats is null)
            throw new ValidationException("Общее количество мест обязательно");

        var eventNew = Event.Create(
            item.Title,
            item.Description,
            item.StartAt,
            item.EndAt,
            item.TotalSeats.Value);

        await _context.Events.AddAsync(eventNew, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ToInfo(eventNew);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.ToLower();
            query = query.Where(item => item.Title.ToLower().Contains(normalizedTitle));
        }

        if (from.HasValue)
            query = query.Where(item => item.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(item => item.EndAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            Count = items.Count,
            Total = total,
            Items = items,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Event> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Событие по Id [{id}] не найдено");

    public async Task<Event> UpdateAsync(
        Guid id,
        EventDto item,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        eventItem.Title = item.Title;
        eventItem.Description = item.Description;
        eventItem.StartAt = item.StartAt;
        eventItem.EndAt = item.EndAt;

        await _context.SaveChangesAsync(cancellationToken);
        return eventItem;
    }

    private async Task<Event> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.Events.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Событие по Id [{id}] не найдено");

    private static EventInfo ToInfo(Event eventItem) => new()
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        StartAt = eventItem.StartAt,
        EndAt = eventItem.EndAt,
        TotalSeats = eventItem.TotalSeats,
        AvailableSeats = eventItem.AvailableSeats
    };
}

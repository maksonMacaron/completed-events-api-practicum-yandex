using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.DTOs;
using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Infrastructure.Persistence.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event> AddAsync(
        Event eventItem,
        CancellationToken cancellationToken = default)
    {
        await _context.Events.AddAsync(eventItem, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return eventItem;
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
            var titlePattern = $"%{title}%";
            query = query.Where(item => EF.Functions.ILike(item.Title, titlePattern));
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

    public async Task<Event?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Event> query = _context.Events;
        if (!trackChanges)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Events.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<BookingConfirmationResult> ApplyBookingConfirmationAsync(
        Guid bookingId,
        Guid eventId,
        int seats,
        DateTime confirmedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (await _context.ProcessedBookingMessages.AnyAsync(
                item => item.BookingId == bookingId,
                cancellationToken))
        {
            return BookingConfirmationResult.AlreadyProcessed;
        }

        var eventItem = await _context.Events.FirstOrDefaultAsync(
            item => item.Id == eventId,
            cancellationToken);

        var result = eventItem switch
        {
            null => BookingConfirmationResult.EventNotFound,
            _ when !eventItem.TryReserveSeats(seats) => BookingConfirmationResult.NotEnoughSeats,
            _ => BookingConfirmationResult.Applied
        };

        _context.ProcessedBookingMessages.Add(
            new ProcessedBookingMessage(bookingId, confirmedAt));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            if (await _context.ProcessedBookingMessages.AnyAsync(
                    item => item.BookingId == bookingId,
                    cancellationToken))
            {
                return BookingConfirmationResult.AlreadyProcessed;
            }

            throw;
        }
    }

    public async Task UpdateAsync(
        Event eventItem,
        CancellationToken cancellationToken = default)
    {
        _context.Events.Update(eventItem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Event eventItem,
        CancellationToken cancellationToken = default)
    {
        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

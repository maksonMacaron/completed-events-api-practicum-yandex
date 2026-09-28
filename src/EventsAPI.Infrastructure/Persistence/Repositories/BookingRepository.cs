using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking> AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return booking;
    }

    public async Task<Booking?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Booking> query = _context.Bookings;
        if (!trackChanges)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetPendingAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetPendingWithEventsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .Include(booking => booking.Event)
            .Where(booking => booking.Status == BookingStatus.Pending)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.Bookings.CountAsync(
            booking => booking.UserId == userId
                && (booking.Status == BookingStatus.Pending
                    || booking.Status == BookingStatus.Confirmed),
            cancellationToken);

    public Task<bool> TryConfirmPendingAsync(
        Guid bookingId,
        DateTime processedAt,
        CancellationToken cancellationToken = default) =>
        TryUpdatePendingStatusAsync(
            bookingId,
            BookingStatus.Confirmed,
            processedAt,
            cancellationToken);

    public Task<bool> TryRejectPendingAsync(
        Guid bookingId,
        DateTime processedAt,
        CancellationToken cancellationToken = default) =>
        TryUpdatePendingStatusAsync(
            bookingId,
            BookingStatus.Rejected,
            processedAt,
            cancellationToken);

    public async Task UpdateAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(booking);
        if (entry.State == EntityState.Detached)
        {
            _context.Bookings.Attach(booking);
            entry.Property(item => item.Status).IsModified = true;
            entry.Property(item => item.ProcessedAt).IsModified = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TryUpdatePendingStatusAsync(
        Guid bookingId,
        BookingStatus status,
        DateTime processedAt,
        CancellationToken cancellationToken)
    {
        var query = _context.Bookings.Where(booking =>
            booking.Id == bookingId && booking.Status == BookingStatus.Pending);

        if (_context.Database.IsRelational())
        {
            var updatedRows = await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(booking => booking.Status, status)
                .SetProperty(booking => booking.ProcessedAt, processedAt),
                cancellationToken);

            return updatedRows == 1;
        }

        var booking = await query.FirstOrDefaultAsync(cancellationToken);
        if (booking is null)
            return false;

        var entry = _context.Entry(booking);
        entry.Property(item => item.Status).CurrentValue = status;
        entry.Property(item => item.ProcessedAt).CurrentValue = processedAt;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

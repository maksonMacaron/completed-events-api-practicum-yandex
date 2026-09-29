using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAPI.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private static readonly TimeSpan PublicationLockDuration = TimeSpan.FromMinutes(5);

    private readonly BookingsDbContext _context;
    private readonly TimeProvider _timeProvider;

    public BookingRepository(BookingsDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
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

    public async Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var bookings = await _context.Bookings
            .FromSqlInterpolated($$"""
                SELECT *
                FROM bookings
                WHERE (
                    status = 'Pending'
                    OR (status = 'Confirmed' AND confirmation_published_at IS NULL)
                    OR (
                        status = 'Cancelled'
                        AND seat_release_required
                        AND (
                            confirmation_published_at IS NULL
                            OR cancellation_published_at IS NULL
                        )
                    )
                )
                AND (
                    publication_locked_until IS NULL
                    OR publication_locked_until < {{now}}
                )
                ORDER BY created_at
                LIMIT 20
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var lockedUntil = now.Add(PublicationLockDuration);
        foreach (var booking in bookings)
            booking.LockPublication(lockedUntil);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return bookings;
    }

    public async Task UpdateAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        _context.Bookings.Update(booking);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

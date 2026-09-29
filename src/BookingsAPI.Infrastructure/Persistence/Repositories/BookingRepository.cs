using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAPI.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly BookingsDbContext _context;

    public BookingRepository(BookingsDbContext context)
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

    public Task<Booking?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _context.Bookings
            .FromSqlInterpolated($"SELECT * FROM bookings WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetPendingForUpdateAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .FromSqlRaw(
                """
                SELECT *
                FROM bookings
                WHERE status = 'Pending'
                ORDER BY created_at
                LIMIT 20
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

    public void AddOutboxMessage(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
    }
}

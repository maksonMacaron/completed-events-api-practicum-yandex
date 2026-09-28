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

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .Where(booking => booking.Status == BookingStatus.Pending)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        _context.Bookings.Update(booking);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

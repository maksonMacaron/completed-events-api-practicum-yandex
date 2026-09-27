using EventsAPI.DataAccess;
using EventsAPI.Exceptions;
using EventsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Services;

/// <summary>Сервис для работы с бронированиями.</summary>
public class BookingService : IBookingService
{
    private static readonly SemaphoreSlim BookingLock = new(1, 1);

    private readonly AppDbContext _context;

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking> CreateBookingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await BookingLock.WaitAsync(cancellationToken);
        try
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Событие по Id [{eventId}] не найдено");

            if (!eventItem.TryReserveSeats())
                throw new NoAvailableSeatsException();

            var booking = new Booking(eventId);
            await _context.Bookings.AddAsync(booking, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            BookingLock.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken)
        ?? throw new KeyNotFoundException($"Бронь по Id [{bookingId}] не найдена");

    public async Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task ConfirmBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        booking.Confirm();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(item => item.Id == booking.EventId, cancellationToken);
        eventItem?.ReleaseSeats();

        booking.Reject();
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Booking> FindByIdAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await _context.Bookings.FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken)
        ?? throw new KeyNotFoundException($"Бронь по Id [{bookingId}] не найдена");
}

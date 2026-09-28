using System.Collections.Concurrent;
using EventsAPI.DataAccess.Repositories;
using EventsAPI.Exceptions;
using EventsAPI.Models;

namespace EventsAPI.Services;

/// <summary>Сервис для работы с бронированиями.</summary>
public class BookingService : IBookingService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> BookingLocks = new();

    private readonly IEventRepository _eventRepository;
    private readonly IBookingRepository _bookingRepository;

    public BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository)
    {
        _eventRepository = eventRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<Booking> CreateBookingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var bookingLock = BookingLocks.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));
        await bookingLock.WaitAsync(cancellationToken);
        try
        {
            var eventItem = await _eventRepository.GetByIdAsync(
                eventId,
                trackChanges: true,
                cancellationToken)
                ?? throw new KeyNotFoundException($"Событие по Id [{eventId}] не найдено");

            if (!eventItem.TryReserveSeats())
                throw new NoAvailableSeatsException();

            var booking = new Booking(eventId);
            await _bookingRepository.AddAsync(booking, cancellationToken);

            return booking;
        }
        finally
        {
            bookingLock.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        await _bookingRepository.GetByIdAsync(bookingId, cancellationToken: cancellationToken)
        ?? throw new KeyNotFoundException($"Бронь по Id [{bookingId}] не найдена");

    public async Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default) =>
        await _bookingRepository.GetPendingAsync(cancellationToken);

    public async Task ConfirmBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        booking.Confirm();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    public async Task RejectBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        var eventItem = await _eventRepository.GetByIdAsync(
            booking.EventId,
            trackChanges: true,
            cancellationToken);
        eventItem?.ReleaseSeats();

        booking.Reject();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private async Task<Booking> FindByIdAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await _bookingRepository.GetByIdAsync(bookingId, trackChanges: true, cancellationToken)
        ?? throw new KeyNotFoundException($"Бронь по Id [{bookingId}] не найдена");
}

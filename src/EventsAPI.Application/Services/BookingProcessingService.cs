using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Services;

public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly TimeProvider _timeProvider;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default) =>
        _bookingRepository.GetPendingWithEventsAsync(cancellationToken);

    public async Task ProcessBookingAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        if (booking.Status != BookingStatus.Pending)
            return;

        if (booking.Event is null)
        {
            booking.Reject(_timeProvider);
            await _bookingRepository.UpdateAsync(booking, cancellationToken);
            return;
        }

        booking.Confirm(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    public async Task RejectAfterFailureAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        if (booking.Status != BookingStatus.Pending)
            return;

        booking.Reject(_timeProvider);
        if (booking.Event is not null)
        {
            booking.Event.ReleaseSeats();
            await _eventRepository.UpdateAsync(booking.Event, cancellationToken);
        }

        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }
}

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
        var processedAt = _timeProvider.GetUtcNow().UtcDateTime;

        if (booking.Event is null)
        {
            await _bookingRepository.TryRejectPendingAsync(
                booking.Id,
                processedAt,
                cancellationToken);
            return;
        }

        await _bookingRepository.TryConfirmPendingAsync(
            booking.Id,
            processedAt,
            cancellationToken);
    }

    public async Task RejectAfterFailureAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        var rejected = await _bookingRepository.TryRejectPendingAsync(
            booking.Id,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (!rejected)
            return;

        if (booking.Event is not null)
        {
            booking.Event.ReleaseSeats();
            await _eventRepository.UpdateAsync(booking.Event, cancellationToken);
        }
    }
}

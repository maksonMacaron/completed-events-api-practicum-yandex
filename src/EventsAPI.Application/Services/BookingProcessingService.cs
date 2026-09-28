using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Services;

public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
    }

    public Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken = default) =>
        _bookingRepository.GetPendingIdsAsync(cancellationToken);

    public async Task ProcessBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            bookingId,
            trackChanges: true,
            cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
            return;

        var eventExists = await _eventRepository.ExistsAsync(
            booking.EventId,
            cancellationToken);

        if (!eventExists)
        {
            booking.Reject();
            await _bookingRepository.UpdateAsync(booking, cancellationToken);
            return;
        }

        booking.Confirm();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    public async Task RejectAfterFailureAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            bookingId,
            trackChanges: true,
            cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
            return;

        booking.Reject();
        var eventItem = await _eventRepository.GetByIdAsync(
            booking.EventId,
            trackChanges: true,
            cancellationToken);
        eventItem?.ReleaseSeats();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }
}

using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Shared.Contracts;

namespace BookingsAPI.Application.Services;

public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingConfirmedPublisher _publisher;
    private readonly TimeProvider _timeProvider;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IBookingConfirmedPublisher publisher,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _publisher = publisher;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
        CancellationToken cancellationToken = default) =>
        _bookingRepository.GetAwaitingPublicationAsync(cancellationToken);

    public async Task ProcessAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        if (booking.Status == BookingStatus.Cancelled
            || booking.ConfirmationPublishedAt.HasValue)
        {
            return;
        }

        if (booking.Status == BookingStatus.Pending)
        {
            booking.Confirm(_timeProvider);
            await _bookingRepository.UpdateAsync(booking, cancellationToken);
        }

        var message = new BookingConfirmed(
            booking.Id,
            booking.EventId,
            booking.UserId,
            booking.Seats,
            booking.ProcessedAt!.Value);

        await _publisher.PublishAsync(message, cancellationToken);
        booking.MarkConfirmationPublished(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }
}

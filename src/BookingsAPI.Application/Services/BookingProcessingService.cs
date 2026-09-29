using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Shared.Contracts;

namespace BookingsAPI.Application.Services;

public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingConfirmedPublisher _confirmedPublisher;
    private readonly IBookingCancelledPublisher _cancelledPublisher;
    private readonly TimeProvider _timeProvider;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IBookingConfirmedPublisher confirmedPublisher,
        IBookingCancelledPublisher cancelledPublisher,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _confirmedPublisher = confirmedPublisher;
        _cancelledPublisher = cancelledPublisher;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
        CancellationToken cancellationToken = default) =>
        _bookingRepository.GetAwaitingPublicationAsync(cancellationToken);

    public async Task ProcessAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        if (booking.Status == BookingStatus.Cancelled)
        {
            await ProcessCancellationAsync(booking, cancellationToken);
            return;
        }

        if (booking.ConfirmationPublishedAt.HasValue)
            return;

        if (booking.Status == BookingStatus.Pending)
        {
            booking.Confirm(_timeProvider);
            await _bookingRepository.UpdateAsync(booking, cancellationToken);
        }

        await PublishConfirmationAsync(booking, releaseLock: true, cancellationToken);
    }

    private async Task ProcessCancellationAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        if (!booking.SeatReleaseRequired)
            return;

        if (!booking.ConfirmationPublishedAt.HasValue)
            await PublishConfirmationAsync(booking, releaseLock: false, cancellationToken);

        if (booking.CancellationPublishedAt.HasValue)
            return;

        var message = new BookingCancelled(
            booking.Id,
            booking.EventId,
            booking.UserId,
            booking.Seats,
            booking.CancelledAt!.Value);

        await _cancelledPublisher.PublishAsync(message, cancellationToken);
        booking.MarkCancellationPublished(_timeProvider);
        booking.ReleasePublicationLock();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private async Task PublishConfirmationAsync(
        Booking booking,
        bool releaseLock,
        CancellationToken cancellationToken)
    {
        var message = new BookingConfirmed(
            booking.Id,
            booking.EventId,
            booking.UserId,
            booking.Seats,
            booking.ConfirmedAt!.Value);

        await _confirmedPublisher.PublishAsync(message, cancellationToken);
        booking.MarkConfirmationPublished(_timeProvider);
        if (releaseLock)
            booking.ReleasePublicationLock();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }
}

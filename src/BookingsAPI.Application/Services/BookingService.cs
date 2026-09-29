using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Application.DTOs;
using BookingsAPI.Domain.Entities;
using BookingsAPI.Domain.Exceptions;
using Shared.Contracts;

namespace BookingsAPI.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventCatalog _eventCatalog;
    private readonly IBookingCancelledPublisher _cancelledPublisher;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IBookingRepository bookingRepository,
        IEventCatalog eventCatalog,
        IBookingCancelledPublisher cancelledPublisher,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _eventCatalog = eventCatalog;
        _cancelledPublisher = cancelledPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateAsync(
        CreateBooking request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await _eventCatalog.ExistsAsync(request.EventId, cancellationToken))
            throw new EventNotFoundException(request.EventId);

        var booking = new Booking(request.EventId, userId, request.Seats, _timeProvider);
        await _bookingRepository.AddAsync(booking, cancellationToken);
        return ToDto(booking);
    }

    public async Task<BookingDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(id, cancellationToken: cancellationToken)
            ?? throw new BookingNotFoundException(id);
        return ToDto(booking);
    }

    public async Task CancelAsync(
        Guid id,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            id,
            trackChanges: true,
            cancellationToken)
            ?? throw new BookingNotFoundException(id);

        if (!isAdmin && booking.UserId != userId)
            throw new ForbiddenOperationException();

        booking.Cancel(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);

        if (!booking.SeatReleaseRequired)
            return;

        if (!booking.ConfirmationPublishedAt.HasValue)
            return;

        await _cancelledPublisher.PublishAsync(
            new BookingCancelled(
                booking.Id,
                booking.EventId,
                booking.UserId,
                booking.Seats,
                booking.CancelledAt!.Value),
            cancellationToken);

        booking.MarkCancellationPublished(_timeProvider);
        booking.ReleasePublicationLock();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private static BookingDto ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        UserId = booking.UserId,
        Seats = booking.Seats,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}

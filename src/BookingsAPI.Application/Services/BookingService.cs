using System.Text.Json;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Application.DTOs;
using BookingsAPI.Domain.Entities;
using BookingsAPI.Domain.Exceptions;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventCatalog _eventCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IBookingRepository bookingRepository,
        IEventCatalog eventCatalog,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _eventCatalog = eventCatalog;
        _unitOfWork = unitOfWork;
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
        await _unitOfWork.ExecuteAsync(async operationToken =>
        {
            var booking = await _bookingRepository.GetByIdForUpdateAsync(id, operationToken)
                ?? throw new BookingNotFoundException(id);

            if (!isAdmin && booking.UserId != userId)
                throw new ForbiddenOperationException();

            booking.Cancel(_timeProvider);
            if (booking.SeatReleaseRequired)
            {
                var message = new BookingCancelled(
                    booking.Id,
                    booking.EventId,
                    booking.UserId,
                    booking.Seats,
                    booking.CancelledAt!.Value);

                _bookingRepository.AddOutboxMessage(new OutboxMessage(
                    booking.Id,
                    nameof(BookingCancelled),
                    JsonSerializer.Serialize(message, KafkaJsonSerializer.Options),
                    message.CancelledAt));
            }

            return booking;
        }, cancellationToken);
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

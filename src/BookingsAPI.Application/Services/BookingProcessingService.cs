using System.Text.Json;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Application.Services;

public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public Task<int> PreparePendingAsync(CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async operationToken =>
        {
            var bookings = await _bookingRepository.GetPendingForUpdateAsync(operationToken);
            foreach (var booking in bookings)
            {
                booking.Confirm(_timeProvider);
                var message = new BookingConfirmed(
                    booking.Id,
                    booking.EventId,
                    booking.UserId,
                    booking.Seats,
                    booking.ConfirmedAt!.Value);

                _bookingRepository.AddOutboxMessage(new OutboxMessage(
                    booking.Id,
                    nameof(BookingConfirmed),
                    JsonSerializer.Serialize(message, KafkaJsonSerializer.Options),
                    message.ConfirmedAt));
            }

            return bookings.Count;
        }, cancellationToken);
}

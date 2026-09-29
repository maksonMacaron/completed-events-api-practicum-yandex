using Shared.Contracts;

namespace BookingsAPI.Application.Abstractions.Messaging;

public interface IBookingCancelledPublisher
{
    Task PublishAsync(
        BookingCancelled message,
        CancellationToken cancellationToken = default);
}

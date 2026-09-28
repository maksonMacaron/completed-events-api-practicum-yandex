using Shared.Contracts;

namespace BookingsAPI.Application.Abstractions.Messaging;

public interface IBookingConfirmedPublisher
{
    Task PublishAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default);
}

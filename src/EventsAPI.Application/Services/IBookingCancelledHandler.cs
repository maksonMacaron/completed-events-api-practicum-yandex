using EventsAPI.Application.Abstractions.Persistence;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public interface IBookingCancelledHandler
{
    Task<BookingCancellationResult> HandleAsync(
        BookingCancelled message,
        CancellationToken cancellationToken = default);
}

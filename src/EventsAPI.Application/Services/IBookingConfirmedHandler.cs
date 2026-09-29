using EventsAPI.Application.Abstractions.Persistence;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public interface IBookingConfirmedHandler
{
    Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default);
}

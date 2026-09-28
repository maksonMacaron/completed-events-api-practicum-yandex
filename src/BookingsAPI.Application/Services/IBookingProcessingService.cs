using BookingsAPI.Domain.Entities;

namespace BookingsAPI.Application.Services;

public interface IBookingProcessingService
{
    Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
        CancellationToken cancellationToken = default);
    Task ProcessAsync(Booking booking, CancellationToken cancellationToken = default);
}

using BookingsAPI.Domain.Entities;

namespace BookingsAPI.Application.Abstractions.Persistence;

public interface IBookingRepository
{
    Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
}

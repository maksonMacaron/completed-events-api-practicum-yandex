using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Abstractions.Persistence;

public interface IBookingRepository
{
    Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task<Booking?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
    Task DeleteAsync(Booking booking, CancellationToken cancellationToken = default);
}

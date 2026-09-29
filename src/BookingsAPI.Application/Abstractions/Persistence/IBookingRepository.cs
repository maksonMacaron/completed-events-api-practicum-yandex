using BookingsAPI.Domain.Entities;

namespace BookingsAPI.Application.Abstractions.Persistence;

public interface IBookingRepository
{
    Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetPendingForUpdateAsync(
        CancellationToken cancellationToken = default);
    void AddOutboxMessage(OutboxMessage message);
}

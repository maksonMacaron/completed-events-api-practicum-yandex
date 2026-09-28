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
    Task<IReadOnlyList<Booking>> GetPendingWithEventsAsync(
        CancellationToken cancellationToken = default);
    Task<int> CountActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<bool> TryConfirmPendingAsync(
        Guid bookingId,
        DateTime processedAt,
        CancellationToken cancellationToken = default);
    Task<bool> TryRejectPendingAsync(
        Guid bookingId,
        DateTime processedAt,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
    Task DeleteAsync(Booking booking, CancellationToken cancellationToken = default);
}

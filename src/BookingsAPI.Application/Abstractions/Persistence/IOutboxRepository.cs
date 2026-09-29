using BookingsAPI.Domain.Entities;

namespace BookingsAPI.Application.Abstractions.Persistence;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        CancellationToken cancellationToken = default);
    Task MarkPublishedAsync(
        long id,
        CancellationToken cancellationToken = default);
}

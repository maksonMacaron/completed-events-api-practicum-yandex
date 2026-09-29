namespace BookingsAPI.Application.Abstractions.Persistence;

public interface IEventCatalog
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task ApplyAsync(
        Guid eventId,
        bool isAvailable,
        DateTime changedAt,
        CancellationToken cancellationToken = default);
}

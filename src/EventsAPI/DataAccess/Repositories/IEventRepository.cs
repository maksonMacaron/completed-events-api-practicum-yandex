using EventsAPI.DTOs;
using EventsAPI.Models;

namespace EventsAPI.DataAccess.Repositories;

public interface IEventRepository
{
    Task<Event> AddAsync(Event eventItem, CancellationToken cancellationToken = default);

    Task<PaginatedResult<Event>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(
        Guid id,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Event eventItem, CancellationToken cancellationToken = default);

    Task DeleteAsync(Event eventItem, CancellationToken cancellationToken = default);
}

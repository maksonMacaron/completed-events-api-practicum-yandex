using EventsAPI.DTOs;
using EventsAPI.Models;

namespace EventsAPI.Services;

public interface IEventService
{
    Task<PaginatedResult<Event>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<Event> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EventInfo> CreateEventAsync(
        CreateEvent item,
        CancellationToken cancellationToken = default);

    Task<Event> UpdateAsync(
        Guid id,
        EventDto item,
        CancellationToken cancellationToken = default);
}

using EventsAPI.Application.DTOs;

namespace EventsAPI.Application.Services;

public interface IEventService
{
    Task<PaginatedResult<EventDto>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventDto>> GetTopAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EventInfo> CreateEventAsync(CreateEvent item, CancellationToken cancellationToken = default);
    Task<EventDto> UpdateAsync(Guid id, EventDto item, CancellationToken cancellationToken = default);
}

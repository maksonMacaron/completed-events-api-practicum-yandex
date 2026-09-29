using EventsAPI.Application.Abstractions.Messaging;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.DTOs;
using EventsAPI.Domain.Entities;
using EventsAPI.Domain.Exceptions;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IEventAvailabilityPublisher _eventAvailabilityPublisher;
    private readonly TimeProvider _timeProvider;

    public EventService(
        IEventRepository eventRepository,
        IEventAvailabilityPublisher eventAvailabilityPublisher,
        TimeProvider timeProvider)
    {
        _eventRepository = eventRepository;
        _eventAvailabilityPublisher = eventAvailabilityPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<EventInfo> CreateEventAsync(
        CreateEvent item,
        CancellationToken cancellationToken = default)
    {
        var eventItem = Event.Create(
            item.Title,
            item.Description,
            item.StartAt,
            item.EndAt,
            item.TotalSeats.GetValueOrDefault());

        await _eventRepository.AddAsync(eventItem, cancellationToken);
        await PublishAvailabilityAsync(eventItem.Id, isAvailable: true, cancellationToken);
        return ToInfo(eventItem);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        await _eventRepository.DeleteAsync(eventItem, cancellationToken);
        await PublishAvailabilityAsync(eventItem.Id, isAvailable: false, cancellationToken);
    }

    public async Task<PaginatedResult<EventDto>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventRepository.GetAllAsync(
            page,
            pageSize,
            title,
            from,
            to,
            cancellationToken);

        return new PaginatedResult<EventDto>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Count = result.Count,
            Total = result.Total,
            Items = result.Items.Select(ToDto).ToList()
        };
    }

    public async Task<EventDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _eventRepository.GetByIdAsync(
            id,
            cancellationToken: cancellationToken)
            ?? throw new EventNotFoundException(id);

        return ToDto(eventItem);
    }

    public async Task<EventDto> UpdateAsync(
        Guid id,
        EventDto item,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        eventItem.UpdateDetails(item.Title, item.Description, item.StartAt, item.EndAt);

        await _eventRepository.UpdateAsync(eventItem, cancellationToken);
        return ToDto(eventItem);
    }

    private async Task<Event> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _eventRepository.GetByIdAsync(id, trackChanges: true, cancellationToken)
        ?? throw new EventNotFoundException(id);

    private Task PublishAvailabilityAsync(
        Guid eventId,
        bool isAvailable,
        CancellationToken cancellationToken) =>
        _eventAvailabilityPublisher.PublishAsync(
            new EventAvailabilityChanged(
                eventId,
                isAvailable,
                _timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

    private static EventDto ToDto(Event eventItem) => new()
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        StartAt = eventItem.StartAt,
        EndAt = eventItem.EndAt,
        TotalSeats = eventItem.TotalSeats,
        AvailableSeats = eventItem.AvailableSeats
    };

    private static EventInfo ToInfo(Event eventItem) => new()
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        StartAt = eventItem.StartAt,
        EndAt = eventItem.EndAt,
        TotalSeats = eventItem.TotalSeats,
        AvailableSeats = eventItem.AvailableSeats
    };
}

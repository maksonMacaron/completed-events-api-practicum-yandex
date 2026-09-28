using System.ComponentModel.DataAnnotations;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.DTOs;
using EventsAPI.Domain.Entities;
using EventsAPI.Domain.Exceptions;

namespace EventsAPI.Application.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<EventInfo> CreateEventAsync(
        CreateEvent item,
        CancellationToken cancellationToken = default)
    {
        if (item.TotalSeats is null)
            throw new ValidationException("Общее количество мест обязательно");

        var eventItem = Event.Create(
            item.Title,
            item.Description,
            item.StartAt,
            item.EndAt,
            item.TotalSeats.Value);

        await _eventRepository.AddAsync(eventItem, cancellationToken);
        return ToInfo(eventItem);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        await _eventRepository.DeleteAsync(eventItem, cancellationToken);
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

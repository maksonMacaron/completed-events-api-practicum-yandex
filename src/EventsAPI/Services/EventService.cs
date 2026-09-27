using System.ComponentModel.DataAnnotations;
using EventsAPI.DataAccess.Repositories;
using EventsAPI.DTOs;
using EventsAPI.Models;

namespace EventsAPI.Services;

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

        var eventNew = Event.Create(
            item.Title,
            item.Description,
            item.StartAt,
            item.EndAt,
            item.TotalSeats.Value);

        await _eventRepository.AddAsync(eventNew, cancellationToken);

        return ToInfo(eventNew);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        await _eventRepository.DeleteAsync(eventItem, cancellationToken);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        int page,
        int pageSize,
        string? title,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
        => await _eventRepository.GetAllAsync(
            page,
            pageSize,
            title,
            from,
            to,
            cancellationToken);

    public async Task<Event> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _eventRepository.GetByIdAsync(id, cancellationToken: cancellationToken)
        ?? throw new KeyNotFoundException($"Событие по Id [{id}] не найдено");

    public async Task<Event> UpdateAsync(
        Guid id,
        EventDto item,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await FindByIdAsync(id, cancellationToken);
        eventItem.Title = item.Title;
        eventItem.Description = item.Description;
        eventItem.StartAt = item.StartAt;
        eventItem.EndAt = item.EndAt;

        await _eventRepository.UpdateAsync(eventItem, cancellationToken);
        return eventItem;
    }

    private async Task<Event> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _eventRepository.GetByIdAsync(id, trackChanges: true, cancellationToken)
        ?? throw new KeyNotFoundException($"Событие по Id [{id}] не найдено");

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

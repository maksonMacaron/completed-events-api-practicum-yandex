using EventsAPI.DTOs;
using EventsAPI.Models;

namespace EventsAPI.Services
{
    public interface IEventService
    {
        PaginatedResult<Event> GetAll(int page, int pageSize, string? title, DateTime? from, DateTime? to);
        Event GetById(Guid id);
        void Delete(Guid id);
        Event Create(Event item);
        Task<EventInfo> CreateEventAsync(CreateEvent item);
        Event Update(Guid id, Event item);
    }
}

using System.Collections.Concurrent;
using EventsAPI.DTOs;
using EventsAPI.Models;

namespace EventsAPI.Services
{
    public class EventService : IEventService
    {
        private readonly ConcurrentDictionary<Guid, Event> _events;

        public EventService()
        {
            var events = new List<Event>
            {
                Event.Create("Мероприятие #1", "Тут описание", DateTime.Now, DateTime.Now.AddDays(5), 100),
                Event.Create("Еще одно какое-то мероприятие", null, DateTime.Now.AddDays(10), DateTime.Now.AddDays(20), 100),
                Event.Create("Концерт Сергея Лазарева", null, DateTime.Now.AddDays(15), DateTime.Now.AddDays(16), 100),
                Event.Create("Спектакль Горе от ума", null, DateTime.Now.AddDays(17), DateTime.Now.AddDays(18), 100),
                Event.Create("Спектакль Алые паруса", null, DateTime.Now.AddDays(22), DateTime.Now.AddDays(23), 100),
                Event.Create("Спектакль Мартышка", null, DateTime.Now.AddDays(28), DateTime.Now.AddDays(30), 100),
                Event.Create("Спектакль Пикова дама", null, DateTime.Now.AddDays(30), DateTime.Now.AddDays(35), 100),
            };

            _events = new ConcurrentDictionary<Guid, Event>(
                events.ToDictionary(item => item.Id));
        }

        public EventService(List<Event> events)
        {
            _events = new ConcurrentDictionary<Guid, Event>(
                events.ToDictionary(item => item.Id));
        }

        public Event Create(Event item)
        {
            var eventNew = Event.Create(item.Title, item.Description, item.StartAt, item.EndAt, item.TotalSeats);
            _events.TryAdd(eventNew.Id, eventNew);
            return eventNew;
        }

        public Task<EventInfo> CreateEventAsync(CreateEvent item)
        {
            if (item.TotalSeats is null)
                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    "Общее количество мест обязательно");

            var eventNew = Event.Create(
                item.Title,
                item.Description,
                item.StartAt,
                item.EndAt,
                item.TotalSeats.Value);

            _events.TryAdd(eventNew.Id, eventNew);

            return Task.FromResult(new EventInfo
            {
                Id = eventNew.Id,
                Title = eventNew.Title,
                Description = eventNew.Description,
                StartAt = eventNew.StartAt,
                EndAt = eventNew.EndAt,
                TotalSeats = eventNew.TotalSeats,
                AvailableSeats = eventNew.AvailableSeats
            });
        }

        public void Delete(Guid id)
        {
            var findEvent = GetById(id);
            _events.TryRemove(findEvent.Id, out _);
        }

        public PaginatedResult<Event> GetAll(int page, int pageSize, string? title, DateTime? from, DateTime? to)
        {
            var query = _events.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

            if (from.HasValue)
                query = query.Where(e => e.StartAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EndAt <= to.Value);

            var total = query.Count();

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<Event>
            {
                Count = items.Count,
                Total = total,
                Items = items,
                Page = page,
                PageSize = pageSize,
            };
        }

        public Event GetById(Guid id)
        {
            if (!_events.TryGetValue(id, out var findEvent))
                throw new KeyNotFoundException($"Событие по Id [{id}] не найдено");

            return findEvent;
        }

        public Event Update(Guid id, Event item)
        {
            var findEvent = GetById(id);

            findEvent.Title = item.Title;
            findEvent.Description = item.Description;
            findEvent.StartAt = item.StartAt;
            findEvent.EndAt = item.EndAt;

            return findEvent;
        }
    }
}

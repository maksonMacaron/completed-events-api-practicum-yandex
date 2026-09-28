namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка отсутствия мероприятия.</summary>
public sealed class EventNotFoundException : KeyNotFoundException
{
    public EventNotFoundException(Guid eventId)
        : base($"Событие по Id [{eventId}] не найдено")
    {
    }
}

namespace BookingsAPI.Domain.Exceptions;

public sealed class EventNotFoundException : KeyNotFoundException
{
    public EventNotFoundException(Guid eventId)
        : base($"Событие с идентификатором {eventId} не найдено")
    {
    }
}

namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка отсутствия свободных мест на мероприятии.</summary>
public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException()
        : base("No available seats for this event")
    {
    }
}

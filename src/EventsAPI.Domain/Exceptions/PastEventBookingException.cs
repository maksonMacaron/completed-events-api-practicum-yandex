namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка попытки забронировать уже начавшееся мероприятие.</summary>
public sealed class PastEventBookingException : Exception
{
    public PastEventBookingException()
        : base("Нельзя забронировать событие, которое уже началось")
    {
    }
}

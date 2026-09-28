namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка отсутствия бронирования.</summary>
public sealed class BookingNotFoundException : KeyNotFoundException
{
    public BookingNotFoundException(Guid bookingId)
        : base($"Бронь по Id [{bookingId}] не найдена")
    {
    }
}

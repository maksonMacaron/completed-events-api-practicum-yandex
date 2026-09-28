namespace BookingsAPI.Domain.Exceptions;

public sealed class BookingNotFoundException : KeyNotFoundException
{
    public BookingNotFoundException(Guid id) : base($"Бронь по Id [{id}] не найдена")
    {
    }
}

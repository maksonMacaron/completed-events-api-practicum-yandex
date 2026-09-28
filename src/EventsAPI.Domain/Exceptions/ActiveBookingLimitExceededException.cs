namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка превышения лимита активных бронирований.</summary>
public sealed class ActiveBookingLimitExceededException : Exception
{
    public ActiveBookingLimitExceededException(int limit)
        : base($"Превышен лимит активных бронирований: {limit}")
    {
        Limit = limit;
    }

    public int Limit { get; }
}

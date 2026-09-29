namespace EventsAPI.Domain.Entities;

/// <summary>Отметка об обработанной отмене брони.</summary>
public sealed class ProcessedBookingCancellation
{
    public Guid BookingId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedBookingCancellation()
    {
    }

    public ProcessedBookingCancellation(Guid bookingId, DateTime processedAt)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Идентификатор брони не может быть пустым", nameof(bookingId));

        BookingId = bookingId;
        ProcessedAt = processedAt;
    }
}

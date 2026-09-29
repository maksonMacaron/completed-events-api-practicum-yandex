namespace EventsAPI.Domain.Entities;

/// <summary>Отметка об обработанном сообщении для защиты от повторного списания мест.</summary>
public sealed class ProcessedBookingMessage
{
    public Guid BookingId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedBookingMessage()
    {
    }

    public ProcessedBookingMessage(Guid bookingId, DateTime processedAt)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Идентификатор брони не может быть пустым", nameof(bookingId));

        BookingId = bookingId;
        ProcessedAt = processedAt;
    }
}

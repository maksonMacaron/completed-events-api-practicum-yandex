namespace EventsAPI.Domain.Entities;

/// <summary>
/// Бронирование мероприятия.
/// </summary>
public class Booking
{
    /// <summary>Уникальный идентификатор брони.</summary>
    public Guid Id { get; private set; }

    /// <summary>Идентификатор мероприятия, к которому относится бронь.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Текущий статус брони.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Дата и время создания брони в UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Дата и время обработки брони в UTC; отсутствует до обработки.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Мероприятие, к которому относится бронь.</summary>
    public Event Event { get; private set; } = null!;

    private Booking()
    {
    }

    /// <summary>Создаёт бронь в статусе ожидания.</summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <exception cref="ArgumentException">Идентификатор мероприятия пустой.</exception>
    public Booking(Guid eventId)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Идентификатор мероприятия не может быть пустым", nameof(eventId));

        Id = Guid.NewGuid();
        EventId = eventId;
        Status = BookingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Подтверждает бронь и фиксирует время обработки.</summary>
    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>Отклоняет бронь и фиксирует время обработки.</summary>
    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }
}

/// <summary>Состояние обработки брони.</summary>
public enum BookingStatus
{
    /// <summary>Бронь ожидает обработки.</summary>
    Pending,
    /// <summary>Бронь подтверждена.</summary>
    Confirmed,
    /// <summary>Бронь отклонена.</summary>
    Rejected
}

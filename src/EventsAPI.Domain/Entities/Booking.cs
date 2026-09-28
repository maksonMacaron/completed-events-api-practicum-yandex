using EventsAPI.Domain.Exceptions;

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

    /// <summary>Идентификатор пользователя, создавшего бронь.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Текущий статус брони.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Дата и время создания брони в UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Дата и время обработки брони в UTC; отсутствует до обработки.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Мероприятие, к которому относится бронь.</summary>
    public Event Event { get; private set; } = null!;

    /// <summary>Пользователь, создавший бронь.</summary>
    public User User { get; private set; } = null!;

    private Booking()
    {
    }

    /// <summary>Создаёт бронь в статусе ожидания.</summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <exception cref="ArgumentException">Один из идентификаторов пустой.</exception>
    public Booking(Guid eventId, Guid userId, TimeProvider timeProvider)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Идентификатор мероприятия не может быть пустым", nameof(eventId));

        if (userId == Guid.Empty)
            throw new ArgumentException("Идентификатор пользователя не может быть пустым", nameof(userId));

        ArgumentNullException.ThrowIfNull(timeProvider);

        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        Status = BookingStatus.Pending;
        CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>Подтверждает бронь и фиксирует время обработки.</summary>
    public void Confirm(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        Status = BookingStatus.Confirmed;
        ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>Отклоняет бронь и фиксирует время обработки.</summary>
    public void Reject(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        Status = BookingStatus.Rejected;
        ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>Отменяет активную бронь и фиксирует время отмены.</summary>
    public void Cancel(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == BookingStatus.Cancelled)
            throw new DomainValidationException("Бронь уже отменена");

        if (Status == BookingStatus.Rejected)
            throw new DomainValidationException("Отклонённую бронь нельзя отменить");

        Status = BookingStatus.Cancelled;
        ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
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
    Rejected,
    /// <summary>Бронь отменена пользователем или администратором.</summary>
    Cancelled
}

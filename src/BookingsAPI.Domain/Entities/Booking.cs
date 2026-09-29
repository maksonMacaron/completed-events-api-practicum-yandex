using BookingsAPI.Domain.Exceptions;

namespace BookingsAPI.Domain.Entities;

public sealed class Booking
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public int Seats { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? ConfirmationPublishedAt { get; private set; }
    public DateTime? CancellationPublishedAt { get; private set; }
    public DateTime? PublicationLockedUntil { get; private set; }
    public bool SeatReleaseRequired { get; private set; }

    private Booking()
    {
    }

    public Booking(Guid eventId, Guid userId, int seats, TimeProvider timeProvider)
    {
        if (eventId == Guid.Empty)
            throw new DomainValidationException("Идентификатор события не может быть пустым");

        if (userId == Guid.Empty)
            throw new DomainValidationException("Идентификатор пользователя не может быть пустым");

        if (seats <= 0)
            throw new DomainValidationException("Количество мест должно быть больше нуля");

        ArgumentNullException.ThrowIfNull(timeProvider);

        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        Seats = seats;
        Status = BookingStatus.Pending;
        CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Confirm(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != BookingStatus.Pending)
            return;

        var confirmedAt = timeProvider.GetUtcNow().UtcDateTime;
        Status = BookingStatus.Confirmed;
        ConfirmedAt = confirmedAt;
        ProcessedAt = confirmedAt;
    }

    public void MarkConfirmationPublished(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status is not (BookingStatus.Confirmed or BookingStatus.Cancelled)
            || ConfirmedAt is null)
        {
            throw new DomainValidationException("Опубликовать можно только подтверждённую бронь");
        }

        ConfirmationPublishedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Cancel(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == BookingStatus.Cancelled)
            throw new DomainValidationException("Бронь уже отменена");

        var cancelledAt = timeProvider.GetUtcNow().UtcDateTime;
        SeatReleaseRequired = Status == BookingStatus.Confirmed;
        Status = BookingStatus.Cancelled;
        CancelledAt = cancelledAt;
        ProcessedAt = cancelledAt;
    }

    public void MarkCancellationPublished(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != BookingStatus.Cancelled || !SeatReleaseRequired)
            throw new DomainValidationException("Компенсация для этой брони не требуется");

        CancellationPublishedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void LockPublication(DateTime lockedUntil)
    {
        PublicationLockedUntil = lockedUntil;
    }

    public void ReleasePublicationLock()
    {
        PublicationLockedUntil = null;
    }
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled
}

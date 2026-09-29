namespace BookingsAPI.Domain.Entities;

public sealed class OutboxMessage
{
    public long Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    private OutboxMessage()
    {
        Type = null!;
        Payload = null!;
    }

    public OutboxMessage(
        Guid bookingId,
        string type,
        string payload,
        DateTime occurredAt)
    {
        BookingId = bookingId;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public void Lock(DateTime lockedUntil)
    {
        LockedUntil = lockedUntil;
    }

    public void MarkPublished(DateTime publishedAt)
    {
        PublishedAt = publishedAt;
        LockedUntil = null;
    }
}

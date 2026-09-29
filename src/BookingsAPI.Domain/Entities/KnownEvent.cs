namespace BookingsAPI.Domain.Entities;

public sealed class KnownEvent
{
    public Guid EventId { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private KnownEvent()
    {
    }

    public KnownEvent(Guid eventId, bool isAvailable, DateTime updatedAt)
    {
        EventId = eventId;
        IsAvailable = isAvailable;
        UpdatedAt = updatedAt;
    }

    public void Apply(bool isAvailable, DateTime updatedAt)
    {
        if (updatedAt < UpdatedAt)
            return;

        IsAvailable = isAvailable;
        UpdatedAt = updatedAt;
    }
}

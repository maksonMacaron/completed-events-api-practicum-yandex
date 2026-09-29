namespace Shared.Contracts;

public sealed record EventAvailabilityChanged(
    Guid EventId,
    bool IsAvailable,
    DateTime ChangedAt);

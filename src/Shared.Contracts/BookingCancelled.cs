namespace Shared.Contracts;

/// <summary>Сообщение об отмене ранее подтверждённой брони.</summary>
public sealed record BookingCancelled(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTime CancelledAt);

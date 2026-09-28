namespace Shared.Contracts;

/// <summary>Сообщение о подтверждённой брони.</summary>
public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTime ConfirmedAt);

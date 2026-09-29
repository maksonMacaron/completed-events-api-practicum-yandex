using BookingsAPI.Domain.Entities;

namespace BookingsAPI.Application.DTOs;

public sealed class BookingDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Guid UserId { get; init; }
    public int Seats { get; init; }
    public BookingStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
}

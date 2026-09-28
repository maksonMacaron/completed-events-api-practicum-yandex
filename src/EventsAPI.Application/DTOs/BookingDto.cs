using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.DTOs;

/// <summary>Информация о бронировании.</summary>
public class BookingDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

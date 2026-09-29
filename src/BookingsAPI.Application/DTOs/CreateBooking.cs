using System.ComponentModel.DataAnnotations;

namespace BookingsAPI.Application.DTOs;

public sealed class CreateBooking
{
    public Guid EventId { get; init; }

    [Range(1, 10, ErrorMessage = "За одну бронь можно выбрать от 1 до 10 мест")]
    public int Seats { get; init; } = 1;
}

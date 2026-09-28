using System.ComponentModel.DataAnnotations;

namespace EventsAPI.Application.DTOs;

/// <summary>Данные мероприятия.</summary>
public class EventDto : IValidatableObject
{
    public Guid Id { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Название события обязательно для заполнения")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Название события должно быть от 3 до 100 символов")]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "Дата начала обязательна")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "Дата окончания обязательна")]
    public DateTime EndAt { get; set; }

    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartAt >= EndAt)
        {
            yield return new ValidationResult(
                "Дата окончания должна быть позже даты начала",
                [nameof(EndAt)]);
        }
    }
}

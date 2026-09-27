using System.ComponentModel.DataAnnotations;

namespace EventsAPI.DTOs
{
    /// <summary>
    /// DTO для передачи данных о мероприятии через API.
    /// </summary>
    public class EventDto : IValidatableObject
    {
        /// <summary>
        /// Уникальный идентификатор мероприятия.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Название мероприятия.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "Название события обязательно для заполнения")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Название события должно быть от 3 до 100 символов")]
        public required string Title { get; set; }

        /// <summary>
        /// Описание мероприятия.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Дата и время начала мероприятия.
        /// </summary>
        [Required(ErrorMessage = "Дата начала обязательна")]
        public DateTime StartAt { get; set; }

        /// <summary>
        /// Дата и время окончания мероприятия.
        /// </summary>
        [Required(ErrorMessage = "Дата окончания обязательна")]
        public DateTime EndAt { get; set; }

        /// <summary>Общее количество мест на мероприятии.</summary>
        public int TotalSeats { get; set; }

        /// <summary>Текущее количество свободных мест.</summary>
        public int AvailableSeats { get; set; }

        /// <summary>
        /// Выполняет дополнительную валидацию модели.
        /// </summary>
        /// <param name="validationContext">Контекст валидации.</param>
        /// <returns>Список ошибок валидации.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartAt >= EndAt)
            {
                yield return new ValidationResult(
                    "Дата окончания должна быть позже даты начала",
                    new[] { nameof(EndAt) });
            }
        }
    }
}

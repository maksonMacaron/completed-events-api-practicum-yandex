using System.ComponentModel.DataAnnotations;

namespace EventsAPI.Domain.Entities
{
    /// <summary>
    /// Модель мероприятия.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Уникальный идентификатор мероприятия.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Название мероприятия.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Описание мероприятия.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Дата и время начала мероприятия.
        /// </summary>
        public DateTime StartAt { get; private set; }

        /// <summary>
        /// Дата и время окончания мероприятия.
        /// </summary>
        public DateTime EndAt { get; private set; }

        /// <summary>Общее количество мест на мероприятии.</summary>
        public int TotalSeats { get; private set; }

        /// <summary>Текущее количество свободных мест на мероприятии.</summary>
        public int AvailableSeats { get; private set; }

        /// <summary>Бронирования мероприятия.</summary>
        public ICollection<Booking> Bookings { get; private set; } = [];

        private Event()
        {
            Title = null!;
        }

        /// <summary>
        /// Создаёт новое мероприятие с указанными параметрами.
        /// </summary>
        /// <param name="title">Название мероприятия.</param>
        /// <param name="description">Описание мероприятия.</param>
        /// <param name="startAt">Дата и время начала.</param>
        /// <param name="endAt">Дата и время окончания.</param>
        /// <param name="totalSeats">Общее количество мест.</param>
        public Event(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
        {
            ValidateDetails(title, startAt, endAt);

            if (totalSeats <= 0)
                throw new ValidationException("Количество мест должно быть больше нуля");

            Id = Guid.NewGuid();
            Title = title;
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
            TotalSeats = totalSeats;
            AvailableSeats = totalSeats;
        }

        /// <summary>Обновляет основные сведения о мероприятии.</summary>
        public void UpdateDetails(
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt)
        {
            ValidateDetails(title, startAt, endAt);

            Title = title;
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
        }

        /// <summary>Создаёт новое мероприятие и проверяет количество мест.</summary>
        public static Event Create(
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt,
            int totalSeats) => new(title, description, startAt, endAt, totalSeats);

        /// <summary>Пытается зарезервировать указанное количество мест.</summary>
        public bool TryReserveSeats(int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля");

            if (AvailableSeats < count)
                return false;

            AvailableSeats -= count;
            return true;
        }

        /// <summary>Освобождает указанное количество ранее зарезервированных мест.</summary>
        public void ReleaseSeats(int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля");

            AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
        }

        private static void ValidateDetails(string title, DateTime startAt, DateTime endAt)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidationException("Название события обязательно для заполнения");

            if (title.Length is < 3 or > 100)
                throw new ValidationException("Название события должно быть от 3 до 100 символов");

            if (startAt >= endAt)
                throw new ValidationException("Дата окончания должна быть позже даты начала");
        }
    }
}

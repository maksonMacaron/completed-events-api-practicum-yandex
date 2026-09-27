using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EventsAPI.Models
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
        public required string Title { get; set; }

        /// <summary>
        /// Описание мероприятия.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Дата и время начала мероприятия.
        /// </summary>
        public DateTime StartAt { get; set; }

        /// <summary>
        /// Дата и время окончания мероприятия.
        /// </summary>
        public DateTime EndAt { get; set; }

        /// <summary>Общее количество мест на мероприятии.</summary>
        public int TotalSeats { get; private set; }

        /// <summary>Текущее количество свободных мест на мероприятии.</summary>
        public int AvailableSeats { get; private set; }

        /// <summary>Бронирования мероприятия.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ICollection<Booking> Bookings { get; private set; } = [];

        private readonly object _seatsLock = new();

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
        [SetsRequiredMembers]
        public Event(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
        {
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

            lock (_seatsLock)
            {
                if (AvailableSeats < count)
                    return false;

                AvailableSeats -= count;
                return true;
            }
        }

        /// <summary>Освобождает указанное количество ранее зарезервированных мест.</summary>
        public void ReleaseSeats(int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля");

            lock (_seatsLock)
            {
                AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
            }
        }
    }
}

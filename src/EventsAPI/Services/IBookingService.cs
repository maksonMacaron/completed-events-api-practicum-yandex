using EventsAPI.Models;

namespace EventsAPI.Services;

/// <summary>Операции создания, получения и обработки бронирований.</summary>
public interface IBookingService
{
    Task<Booking> CreateBookingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default);

    Task ConfirmBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task RejectBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}

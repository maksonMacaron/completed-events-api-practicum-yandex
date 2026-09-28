using EventsAPI.Application.DTOs;

namespace EventsAPI.Application.Services;

/// <summary>Операции создания, получения и обработки бронирований.</summary>
public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingDto>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task ConfirmBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task RejectBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

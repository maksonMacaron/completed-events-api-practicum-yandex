using BookingsAPI.Application.DTOs;

namespace BookingsAPI.Application.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(
        CreateBooking request,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<BookingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelAsync(
        Guid id,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Services;

/// <summary>Операции фоновой обработки ожидающих бронирований.</summary>
public interface IBookingProcessingService
{
    Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default);

    Task ProcessBookingAsync(
        Booking booking,
        CancellationToken cancellationToken = default);

    Task RejectAfterFailureAsync(
        Booking booking,
        CancellationToken cancellationToken = default);
}

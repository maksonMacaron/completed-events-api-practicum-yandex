namespace EventsAPI.Application.Services;

/// <summary>Операции фоновой обработки ожидающих бронирований.</summary>
public interface IBookingProcessingService
{
    Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken = default);

    Task ProcessBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task RejectAfterFailureAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}

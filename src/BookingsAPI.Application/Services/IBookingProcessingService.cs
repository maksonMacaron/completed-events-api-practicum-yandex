namespace BookingsAPI.Application.Services;

public interface IBookingProcessingService
{
    Task<int> PreparePendingAsync(CancellationToken cancellationToken = default);
}

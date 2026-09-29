using EventsAPI.Application.Abstractions.Caching;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Caching;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public sealed class BookingCancelledHandler : IBookingCancelledHandler
{
    private readonly IEventRepository _eventRepository;
    private readonly ICacheService _cache;

    public BookingCancelledHandler(
        IEventRepository eventRepository,
        ICacheService cache)
    {
        _eventRepository = eventRepository;
        _cache = cache;
    }

    public async Task<BookingCancellationResult> HandleAsync(
        BookingCancelled message,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventRepository.ApplyBookingCancellationAsync(
            message.BookingId,
            message.EventId,
            message.Seats,
            message.CancelledAt,
            cancellationToken);

        if (result == BookingCancellationResult.Released)
        {
            await _cache.RemoveAsync(
                EventCacheKeys.ById(message.EventId),
                cancellationToken);
        }

        return result;
    }
}

using EventsAPI.Application.Abstractions.Caching;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Caching;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly IEventRepository _eventRepository;
    private readonly ICacheService _cache;

    public BookingConfirmedHandler(
        IEventRepository eventRepository,
        ICacheService cache)
    {
        _eventRepository = eventRepository;
        _cache = cache;
    }

    public async Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventRepository.ApplyBookingConfirmationAsync(
            message.BookingId,
            message.EventId,
            message.Seats,
            message.ConfirmedAt,
            cancellationToken);

        if (result == BookingConfirmationResult.Applied)
        {
            await _cache.RemoveAsync(
                EventCacheKeys.ById(message.EventId),
                cancellationToken);
        }

        return result;
    }
}

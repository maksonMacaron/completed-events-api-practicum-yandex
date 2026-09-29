using EventsAPI.Application.Abstractions.Persistence;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public sealed class BookingCancelledHandler : IBookingCancelledHandler
{
    private readonly IEventRepository _eventRepository;

    public BookingCancelledHandler(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public Task<BookingCancellationResult> HandleAsync(
        BookingCancelled message,
        CancellationToken cancellationToken = default) =>
        _eventRepository.ApplyBookingCancellationAsync(
            message.BookingId,
            message.EventId,
            message.Seats,
            message.CancelledAt,
            cancellationToken);
}

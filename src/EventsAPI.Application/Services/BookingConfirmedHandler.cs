using EventsAPI.Application.Abstractions.Persistence;
using Shared.Contracts;

namespace EventsAPI.Application.Services;

public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly IEventRepository _eventRepository;

    public BookingConfirmedHandler(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default) =>
        _eventRepository.ApplyBookingConfirmationAsync(
            message.BookingId,
            message.EventId,
            message.Seats,
            message.ConfirmedAt,
            cancellationToken);
}

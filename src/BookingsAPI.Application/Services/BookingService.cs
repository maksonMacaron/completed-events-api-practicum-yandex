using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Application.DTOs;
using BookingsAPI.Domain.Entities;
using BookingsAPI.Domain.Exceptions;

namespace BookingsAPI.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IBookingRepository bookingRepository,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateAsync(
        CreateBooking request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var booking = new Booking(request.EventId, userId, request.Seats, _timeProvider);
        await _bookingRepository.AddAsync(booking, cancellationToken);
        return ToDto(booking);
    }

    public async Task<BookingDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(id, cancellationToken: cancellationToken)
            ?? throw new BookingNotFoundException(id);
        return ToDto(booking);
    }

    public async Task CancelAsync(
        Guid id,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            id,
            trackChanges: true,
            cancellationToken)
            ?? throw new BookingNotFoundException(id);

        if (!isAdmin && booking.UserId != userId)
            throw new ForbiddenOperationException();

        booking.Cancel(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private static BookingDto ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        UserId = booking.UserId,
        Seats = booking.Seats,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}

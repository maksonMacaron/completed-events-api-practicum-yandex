using System.Collections.Concurrent;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.DTOs;
using EventsAPI.Domain.Entities;
using EventsAPI.Domain.Exceptions;

namespace EventsAPI.Application.Services;

/// <summary>Сервис для работы с бронированиями.</summary>
public class BookingService : IBookingService
{
    public const int ActiveBookingLimit = 10;

    private static readonly ConcurrentDictionary<Guid, BookingLock> BookingLocks = new();

    private readonly IEventRepository _eventRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository,
        TimeProvider timeProvider)
    {
        _eventRepository = eventRepository;
        _bookingRepository = bookingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> CreateBookingAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var userLock = await AcquireBookingLockAsync(userId, cancellationToken);
        using var eventLock = await AcquireBookingLockAsync(eventId, cancellationToken);
        var eventItem = await _eventRepository.GetByIdAsync(
            eventId,
            trackChanges: true,
            cancellationToken)
            ?? throw new EventNotFoundException(eventId);

        if (eventItem.StartAt <= _timeProvider.GetUtcNow().UtcDateTime)
            throw new PastEventBookingException();

        var activeBookingsCount = await _bookingRepository.CountActiveByUserIdAsync(
            userId,
            cancellationToken);
        if (activeBookingsCount >= ActiveBookingLimit)
            throw new ActiveBookingLimitExceededException(ActiveBookingLimit);

        if (!eventItem.TryReserveSeats())
            throw new NoAvailableSeatsException();

        var booking = new Booking(eventId, userId, _timeProvider);
        await _bookingRepository.AddAsync(booking, cancellationToken);
        return ToDto(booking);
    }

    public async Task<BookingDto> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(
            bookingId,
            cancellationToken: cancellationToken)
            ?? throw new BookingNotFoundException(bookingId);

        return ToDto(booking);
    }

    public async Task CancelBookingAsync(
        Guid bookingId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        using var bookingLock = await AcquireBookingLockAsync(bookingId, cancellationToken);
        var booking = await FindByIdAsync(bookingId, cancellationToken);

        if (!isAdmin && booking.UserId != userId)
            throw new ForbiddenOperationException();

        using var eventLock = await AcquireBookingLockAsync(booking.EventId, cancellationToken);
        var shouldReleaseSeat = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed;
        booking.Cancel(_timeProvider);

        if (shouldReleaseSeat)
        {
            var eventItem = await _eventRepository.GetByIdAsync(
                booking.EventId,
                trackChanges: true,
                cancellationToken);
            eventItem?.ReleaseSeats();
        }

        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    public async Task<IReadOnlyList<BookingDto>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetPendingAsync(cancellationToken);
        return bookings.Select(ToDto).ToList();
    }

    public async Task ConfirmBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        booking.Confirm(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    public async Task RejectBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindByIdAsync(bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Pending)
            return;

        var eventItem = await _eventRepository.GetByIdAsync(
            booking.EventId,
            trackChanges: true,
            cancellationToken);
        eventItem?.ReleaseSeats();

        booking.Reject(_timeProvider);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private async Task<Booking> FindByIdAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await _bookingRepository.GetByIdAsync(bookingId, trackChanges: true, cancellationToken)
        ?? throw new BookingNotFoundException(bookingId);

    private static async Task<BookingLockLease> AcquireBookingLockAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var bookingLock = BookingLocks.GetOrAdd(eventId, _ => new BookingLock());
            lock (bookingLock.SyncRoot)
            {
                if (bookingLock.IsRetired)
                    continue;

                bookingLock.ReferenceCount++;
            }

            try
            {
                await bookingLock.Semaphore.WaitAsync(cancellationToken);
                return new BookingLockLease(eventId, bookingLock);
            }
            catch
            {
                ReleaseBookingLock(eventId, bookingLock, releaseSemaphore: false);
                throw;
            }
        }
    }

    private static void ReleaseBookingLock(
        Guid eventId,
        BookingLock bookingLock,
        bool releaseSemaphore)
    {
        if (releaseSemaphore)
            bookingLock.Semaphore.Release();

        var remove = false;
        lock (bookingLock.SyncRoot)
        {
            bookingLock.ReferenceCount--;
            if (bookingLock.ReferenceCount == 0)
            {
                bookingLock.IsRetired = true;
                remove = true;
            }
        }

        if (remove && BookingLocks.TryRemove(
                new KeyValuePair<Guid, BookingLock>(eventId, bookingLock)))
        {
            bookingLock.Semaphore.Dispose();
        }
    }

    private static BookingDto ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        UserId = booking.UserId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };

    private sealed class BookingLock
    {
        public object SyncRoot { get; } = new();
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
        public bool IsRetired { get; set; }
    }

    private sealed class BookingLockLease : IDisposable
    {
        private readonly Guid _eventId;
        private BookingLock? _bookingLock;

        public BookingLockLease(Guid eventId, BookingLock bookingLock)
        {
            _eventId = eventId;
            _bookingLock = bookingLock;
        }

        public void Dispose()
        {
            var bookingLock = Interlocked.Exchange(ref _bookingLock, null);
            if (bookingLock is not null)
                ReleaseBookingLock(_eventId, bookingLock, releaseSemaphore: true);
        }
    }
}

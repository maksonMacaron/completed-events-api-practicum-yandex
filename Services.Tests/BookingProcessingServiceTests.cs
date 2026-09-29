using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Application.DTOs;
using BookingsAPI.Application.Services;
using BookingsAPI.Domain.Entities;
using BookingsAPI.Domain.Exceptions;
using Shared.Contracts;

namespace Services.Tests;

public sealed class BookingProcessingServiceTests
{
    [Fact]
    public async Task PreparePendingAsync_ConfirmsBookingAndAddsOutboxMessage()
    {
        var timeProvider = CreateTimeProvider();
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        var repository = new BookingRepositoryStub { PendingBookings = [booking] };
        var unitOfWork = new UnitOfWorkStub();
        var service = new BookingProcessingService(repository, unitOfWork, timeProvider);

        var processedCount = await service.PreparePendingAsync();

        Assert.Equal(1, processedCount);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Single(repository.OutboxMessages);
        Assert.Equal(nameof(BookingConfirmed), repository.OutboxMessages[0].Type);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task CancelAsync_AddsCompensationToSameTransaction()
    {
        var timeProvider = CreateTimeProvider();
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        booking.Confirm(timeProvider);

        var repository = new BookingRepositoryStub { BookingToReturn = booking };
        var unitOfWork = new UnitOfWorkStub();
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            unitOfWork,
            timeProvider);

        await service.CancelAsync(booking.Id, booking.UserId, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Single(repository.OutboxMessages);
        Assert.Equal(nameof(BookingCancelled), repository.OutboxMessages[0].Type);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task CancelAsync_DoesNotCreateCompensationForPendingBooking()
    {
        var timeProvider = CreateTimeProvider();
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        var repository = new BookingRepositoryStub { BookingToReturn = booking };
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            new UnitOfWorkStub(),
            timeProvider);

        await service.CancelAsync(booking.Id, booking.UserId, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Empty(repository.OutboxMessages);
    }

    [Fact]
    public async Task CancelAsync_DoesNotCreateDuplicateCancellation()
    {
        var timeProvider = CreateTimeProvider();
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        booking.Confirm(timeProvider);
        var repository = new BookingRepositoryStub { BookingToReturn = booking };
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            new UnitOfWorkStub(),
            timeProvider);

        await service.CancelAsync(booking.Id, booking.UserId, isAdmin: false);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CancelAsync(booking.Id, booking.UserId, isAdmin: false));
        Assert.Single(repository.OutboxMessages);
    }

    [Fact]
    public async Task ConfirmationIsQueuedBeforeCancellation()
    {
        var timeProvider = CreateTimeProvider();
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        var repository = new BookingRepositoryStub
        {
            BookingToReturn = booking,
            PendingBookings = [booking]
        };
        var unitOfWork = new UnitOfWorkStub();
        var processingService = new BookingProcessingService(repository, unitOfWork, timeProvider);
        var bookingService = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            unitOfWork,
            timeProvider);

        await processingService.PreparePendingAsync();
        await bookingService.CancelAsync(booking.Id, booking.UserId, isAdmin: false);

        Assert.Equal(
            [nameof(BookingConfirmed), nameof(BookingCancelled)],
            repository.OutboxMessages.Select(message => message.Type));
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownEvent()
    {
        var timeProvider = CreateTimeProvider();
        var repository = new BookingRepositoryStub();
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: false),
            new UnitOfWorkStub(),
            timeProvider);
        var request = new CreateBooking
        {
            EventId = Guid.NewGuid(),
            Seats = 1
        };

        await Assert.ThrowsAsync<EventNotFoundException>(() =>
            service.CreateAsync(request, Guid.NewGuid()));
    }

    private static FixedTimeProvider CreateTimeProvider() =>
        new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return await operation(cancellationToken);
        }
    }

    private sealed class EventCatalogStub : IEventCatalog
    {
        private readonly bool _eventExists;

        public EventCatalogStub(bool eventExists)
        {
            _eventExists = eventExists;
        }

        public Task<bool> ExistsAsync(
            Guid eventId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_eventExists);

        public Task ApplyAsync(
            Guid eventId,
            bool isAvailable,
            DateTime changedAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> RemoveUnavailableBeforeAsync(
            DateTime threshold,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class BookingRepositoryStub : IBookingRepository
    {
        public Booking? BookingToReturn { get; init; }
        public IReadOnlyList<Booking> PendingBookings { get; init; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<Booking> AddAsync(
            Booking booking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(booking);

        public Task<Booking?> GetByIdAsync(
            Guid id,
            bool trackChanges = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BookingToReturn);

        public Task<Booking?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BookingToReturn);

        public Task<IReadOnlyList<Booking>> GetPendingForUpdateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PendingBookings);

        public void AddOutboxMessage(OutboxMessage message)
        {
            OutboxMessages.Add(message);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

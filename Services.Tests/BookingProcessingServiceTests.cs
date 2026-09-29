using BookingsAPI.Application.Abstractions.Messaging;
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
    public async Task ProcessAsync_SavesConfirmationBeforePublishing()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        var repository = new BookingRepositoryStub();
        var publisher = new PublisherStub(repository);
        var service = new BookingProcessingService(
            repository,
            publisher,
            publisher,
            timeProvider);

        await service.ProcessAsync(booking);

        Assert.True(publisher.ConfirmationWasPublished);
        Assert.Equal([1], publisher.ConfirmationUpdateCounts);
        Assert.Equal(2, repository.UpdateCount);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ConfirmationPublishedAt);
    }

    [Fact]
    public async Task CancelAsync_SavesCancellationBeforePublishingCompensation()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        booking.Confirm(timeProvider);
        booking.MarkConfirmationPublished(timeProvider);

        var repository = new BookingRepositoryStub { BookingToReturn = booking };
        var publisher = new PublisherStub(repository);
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            publisher,
            timeProvider);

        await service.CancelAsync(booking.Id, booking.UserId, isAdmin: false);

        Assert.True(publisher.CancellationWasPublished);
        Assert.Equal([1], publisher.CancellationUpdateCounts);
        Assert.Equal(2, repository.UpdateCount);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancellationPublishedAt);
    }

    [Fact]
    public async Task ProcessAsync_PublishesConfirmationBeforeEarlyCancellation()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2, timeProvider);
        booking.Confirm(timeProvider);

        var repository = new BookingRepositoryStub { BookingToReturn = booking };
        var publisher = new PublisherStub(repository);
        var bookingService = new BookingService(
            repository,
            new EventCatalogStub(eventExists: true),
            publisher,
            timeProvider);
        var processingService = new BookingProcessingService(
            repository,
            publisher,
            publisher,
            timeProvider);

        await bookingService.CancelAsync(booking.Id, booking.UserId, isAdmin: false);
        Assert.False(publisher.CancellationWasPublished);

        await processingService.ProcessAsync(booking);

        Assert.Equal(["confirmed", "cancelled"], publisher.PublishedMessages);
        Assert.Equal([1], publisher.ConfirmationUpdateCounts);
        Assert.Equal([2], publisher.CancellationUpdateCounts);
        Assert.NotNull(booking.ConfirmationPublishedAt);
        Assert.NotNull(booking.CancellationPublishedAt);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownEvent()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var repository = new BookingRepositoryStub();
        var publisher = new PublisherStub(repository);
        var service = new BookingService(
            repository,
            new EventCatalogStub(eventExists: false),
            publisher,
            timeProvider);
        var request = new CreateBooking
        {
            EventId = Guid.NewGuid(),
            Seats = 1
        };

        await Assert.ThrowsAsync<EventNotFoundException>(() =>
            service.CreateAsync(request, Guid.NewGuid()));
    }

    private sealed class PublisherStub : IBookingConfirmedPublisher, IBookingCancelledPublisher
    {
        private readonly BookingRepositoryStub _repository;

        public PublisherStub(BookingRepositoryStub repository)
        {
            _repository = repository;
        }

        public bool ConfirmationWasPublished { get; private set; }
        public bool CancellationWasPublished { get; private set; }
        public List<int> ConfirmationUpdateCounts { get; } = [];
        public List<int> CancellationUpdateCounts { get; } = [];
        public List<string> PublishedMessages { get; } = [];

        public Task PublishAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            ConfirmationWasPublished = true;
            ConfirmationUpdateCounts.Add(_repository.UpdateCount);
            PublishedMessages.Add("confirmed");
            return Task.CompletedTask;
        }

        public Task PublishAsync(
            BookingCancelled message,
            CancellationToken cancellationToken = default)
        {
            CancellationWasPublished = true;
            CancellationUpdateCounts.Add(_repository.UpdateCount);
            PublishedMessages.Add("cancelled");
            return Task.CompletedTask;
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
    }

    private sealed class BookingRepositoryStub : IBookingRepository
    {
        public int UpdateCount { get; private set; }
        public Booking? BookingToReturn { get; init; }

        public Task<Booking> AddAsync(
            Booking booking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(booking);

        public Task<Booking?> GetByIdAsync(
            Guid id,
            bool trackChanges = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BookingToReturn);

        public Task<IReadOnlyList<Booking>> GetAwaitingPublicationAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Booking>>([]);

        public Task UpdateAsync(
            Booking booking,
            CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
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

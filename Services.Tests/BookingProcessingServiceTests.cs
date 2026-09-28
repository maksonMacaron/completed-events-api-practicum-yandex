using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Application.Services;
using BookingsAPI.Domain.Entities;
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
        var service = new BookingProcessingService(repository, publisher, timeProvider);

        await service.ProcessAsync(booking);

        Assert.True(publisher.WasCalled);
        Assert.Equal(2, repository.UpdateCount);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ConfirmationPublishedAt);
    }

    private sealed class PublisherStub : IBookingConfirmedPublisher
    {
        private readonly BookingRepositoryStub _repository;

        public PublisherStub(BookingRepositoryStub repository)
        {
            _repository = repository;
        }

        public bool WasCalled { get; private set; }

        public Task PublishAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(1, _repository.UpdateCount);
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class BookingRepositoryStub : IBookingRepository
    {
        public int UpdateCount { get; private set; }

        public Task<Booking> AddAsync(
            Booking booking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(booking);

        public Task<Booking?> GetByIdAsync(
            Guid id,
            bool trackChanges = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Booking?>(null);

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

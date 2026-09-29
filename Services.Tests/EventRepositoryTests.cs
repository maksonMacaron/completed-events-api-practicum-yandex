using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Domain.Entities;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Services.Tests;

public sealed class EventRepositoryTests
{
    [Fact]
    public async Task ApplyBookingConfirmationAsync_DoesNotMarkMissingEventAsProcessed()
    {
        await using var fixture = await EventRepositoryFixture.CreateAsync(totalSeats: 1);

        var result = await fixture.Repository.ApplyBookingConfirmationAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            seats: 1,
            DateTime.UtcNow);

        Assert.Equal(BookingConfirmationResult.EventNotFound, result);
        Assert.Empty(await fixture.Context.ProcessedBookingMessages.ToListAsync());
    }

    [Fact]
    public async Task ApplyBookingConfirmationAsync_DoesNotMarkFailedMessageAsProcessed()
    {
        await using var fixture = await EventRepositoryFixture.CreateAsync(totalSeats: 1);
        var bookingId = Guid.NewGuid();

        var result = await fixture.Repository.ApplyBookingConfirmationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);

        Assert.Equal(BookingConfirmationResult.NotEnoughSeats, result);
        Assert.Equal(1, fixture.Event.AvailableSeats);
        Assert.Empty(await fixture.Context.ProcessedBookingMessages.ToListAsync());
    }

    [Fact]
    public async Task ApplyBookingConfirmationAsync_DecreasesSeatsOnlyOnce()
    {
        await using var fixture = await EventRepositoryFixture.CreateAsync(totalSeats: 5);
        var bookingId = Guid.NewGuid();

        var firstResult = await fixture.Repository.ApplyBookingConfirmationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);
        var secondResult = await fixture.Repository.ApplyBookingConfirmationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);

        Assert.Equal(BookingConfirmationResult.Applied, firstResult);
        Assert.Equal(BookingConfirmationResult.AlreadyProcessed, secondResult);
        Assert.Equal(3, fixture.Event.AvailableSeats);
    }

    [Fact]
    public async Task ApplyBookingCancellationAsync_ReleasesSeatsOnlyOnce()
    {
        await using var fixture = await EventRepositoryFixture.CreateAsync(totalSeats: 5);
        var bookingId = Guid.NewGuid();
        await fixture.Repository.ApplyBookingConfirmationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);

        var firstResult = await fixture.Repository.ApplyBookingCancellationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);
        var secondResult = await fixture.Repository.ApplyBookingCancellationAsync(
            bookingId,
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);

        Assert.Equal(BookingCancellationResult.Released, firstResult);
        Assert.Equal(BookingCancellationResult.AlreadyProcessed, secondResult);
        Assert.Equal(5, fixture.Event.AvailableSeats);
    }

    [Fact]
    public async Task ApplyBookingCancellationAsync_WaitsForConfirmation()
    {
        await using var fixture = await EventRepositoryFixture.CreateAsync(totalSeats: 5);

        var result = await fixture.Repository.ApplyBookingCancellationAsync(
            Guid.NewGuid(),
            fixture.Event.Id,
            seats: 2,
            DateTime.UtcNow);

        Assert.Equal(BookingCancellationResult.ConfirmationNotProcessed, result);
        Assert.Equal(5, fixture.Event.AvailableSeats);
        Assert.Empty(await fixture.Context.ProcessedBookingCancellations.ToListAsync());
    }

    private sealed class EventRepositoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private EventRepositoryFixture(
            SqliteConnection connection,
            AppDbContext context,
            EventRepository repository,
            Event eventItem)
        {
            _connection = connection;
            Context = context;
            Repository = repository;
            Event = eventItem;
        }

        public AppDbContext Context { get; }
        public EventRepository Repository { get; }
        public Event Event { get; }

        public static async Task<EventRepositoryFixture> CreateAsync(int totalSeats)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var repository = new EventRepository(context);
            var eventItem = Event.Create(
                "Концерт",
                null,
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(2),
                totalSeats);
            await repository.AddAsync(eventItem);

            return new EventRepositoryFixture(connection, context, repository, eventItem);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

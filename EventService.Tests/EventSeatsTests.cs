using System.ComponentModel.DataAnnotations;
using EventsAPI.DTOs;
using EventsAPI.Models;
using EventsAPI.Services;

namespace EventsAPI.Tests;

public class EventSeatsTests
{
    [Fact]
    public void Create_WithPositiveTotalSeats_InitializesAvailableSeats()
    {
        var eventItem = NewEvent(5);

        Assert.Equal(5, eventItem.TotalSeats);
        Assert.Equal(5, eventItem.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
    {
        Assert.Throws<ValidationException>(() => NewEvent(totalSeats));
    }

    [Fact]
    public void TryReserveSeats_WhenSeatsAreAvailable_DecreasesAvailableSeats()
    {
        var eventItem = NewEvent(3);

        var reserved = eventItem.TryReserveSeats(2);

        Assert.True(reserved);
        Assert.Equal(1, eventItem.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_WhenSeatsAreInsufficient_DoesNotChangeAvailableSeats()
    {
        var eventItem = NewEvent(1);

        var reserved = eventItem.TryReserveSeats(2);

        Assert.False(reserved);
        Assert.Equal(1, eventItem.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_IncreasesAvailableSeatsWithoutExceedingTotalSeats()
    {
        var eventItem = NewEvent(2);
        eventItem.TryReserveSeats(2);

        eventItem.ReleaseSeats(3);

        Assert.Equal(2, eventItem.AvailableSeats);
    }

    [Fact]
    public async Task CreateEventAsync_ReturnsSeatCounts()
    {
        var service = new EventService([]);
        var request = new CreateEvent
        {
            Title = "Концерт",
            StartAt = new DateTime(2026, 10, 1),
            EndAt = new DateTime(2026, 10, 2),
            TotalSeats = 25
        };

        var result = await service.CreateEventAsync(request);

        Assert.Equal(25, result.TotalSeats);
        Assert.Equal(25, result.AvailableSeats);
        Assert.Equal(25, service.GetById(result.Id).AvailableSeats);
    }

    private static Event NewEvent(int totalSeats) => Event.Create(
        "Концерт",
        null,
        new DateTime(2026, 10, 1),
        new DateTime(2026, 10, 2),
        totalSeats);
}

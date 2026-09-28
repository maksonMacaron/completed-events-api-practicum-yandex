using EventsAPI.Domain.Entities;

namespace Services.Tests;

public sealed class EventTests
{
    [Fact]
    public void TryReserveSeats_DecreasesAvailableSeats()
    {
        var eventItem = Event.Create(
            "Концерт",
            null,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            10);

        var reserved = eventItem.TryReserveSeats(3);

        Assert.True(reserved);
        Assert.Equal(7, eventItem.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_DoesNotChangeState_WhenSeatsAreNotAvailable()
    {
        var eventItem = Event.Create(
            "Концерт",
            null,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            2);

        var reserved = eventItem.TryReserveSeats(3);

        Assert.False(reserved);
        Assert.Equal(2, eventItem.AvailableSeats);
    }
}

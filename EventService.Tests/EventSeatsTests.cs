using System.ComponentModel.DataAnnotations;
using EventsAPI.Domain.Entities;

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
    public void ReleaseSeats_DoesNotExceedTotalSeats()
    {
        var eventItem = NewEvent(2);
        eventItem.TryReserveSeats(2);

        eventItem.ReleaseSeats(3);

        Assert.Equal(2, eventItem.AvailableSeats);
    }

    [Fact]
    public void UpdateDetails_WhenEndIsBeforeStart_ThrowsValidationException()
    {
        var eventItem = NewEvent(2);
        var startAt = DateTime.UtcNow.AddDays(3);

        Assert.Throws<ValidationException>(() => eventItem.UpdateDetails(
            "Обновлённый концерт",
            null,
            startAt,
            startAt.AddHours(-1)));
    }

    private static Event NewEvent(int totalSeats) => Event.Create(
        "Концерт",
        null,
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow.AddDays(2),
        totalSeats);
}

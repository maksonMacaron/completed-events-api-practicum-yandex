namespace Shared.Contracts;

public static class KafkaTopics
{
    public const string BookingConfirmed = "booking-confirmed";
    public const string BookingCancelled = "booking-cancelled";
    public const string EventAvailabilityChanged = "event-availability-changed";
}

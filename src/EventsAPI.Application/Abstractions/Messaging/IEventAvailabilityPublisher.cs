using Shared.Contracts;

namespace EventsAPI.Application.Abstractions.Messaging;

public interface IEventAvailabilityPublisher
{
    Task PublishAsync(
        EventAvailabilityChanged message,
        CancellationToken cancellationToken = default);
}

using System.Text.Json;

namespace Shared.Contracts.Infrastructure;

public static class KafkaJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

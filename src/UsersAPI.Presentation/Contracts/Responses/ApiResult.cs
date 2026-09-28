using System.Net;

namespace UsersAPI.Presentation.Contracts.Responses;

public class ApiBaseResult
{
    public required bool Success { get; set; }
    public required HttpStatusCode StatusCode { get; set; }
    public required DateTime DateTime { get; set; }
    public string? Message { get; set; }
}

public sealed class ApiResult : ApiBaseResult
{
}

public sealed class ApiResult<T> : ApiBaseResult
{
    public required T? Data { get; set; }
}

public sealed class ValidationApiResult : ApiBaseResult
{
    public required Dictionary<string, IEnumerable<string>> Errors { get; set; }
}

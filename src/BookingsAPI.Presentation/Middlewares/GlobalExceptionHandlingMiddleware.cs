using System.Net;
using BookingsAPI.Domain.Exceptions;
using Shared.Contracts.Responses;

namespace BookingsAPI.Presentation.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly TimeProvider _timeProvider;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        TimeProvider timeProvider)
    {
        _next = next;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Необработанная ошибка при выполнении {Path}", context.Request.Path);

            if (context.Response.HasStarted)
                return;

            var statusCode = exception switch
            {
                DomainValidationException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ForbiddenOperationException => StatusCodes.Status403Forbidden,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiResult
            {
                StatusCode = (HttpStatusCode)statusCode,
                Success = false,
                Message = exception.Message,
                DateTime = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
    }
}

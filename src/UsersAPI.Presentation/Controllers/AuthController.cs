using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersAPI.Application.DTOs;
using UsersAPI.Application.Services;
using UsersAPI.Presentation.Contracts.Responses;

namespace UsersAPI.Presentation.Controllers;

[Route("auth")]
[ApiController]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly TimeProvider _timeProvider;

    public AuthController(IAuthService authService, TimeProvider timeProvider)
    {
        _authService = authService;
        _timeProvider = timeProvider;
    }

    /// <summary>Зарегистрировать нового пользователя.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationApiResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterUser request,
        CancellationToken cancellationToken = default)
    {
        await _authService.RegisterAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Получить JWT-токен.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResult<AuthToken>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginUser request,
        CancellationToken cancellationToken = default)
    {
        var token = await _authService.LoginAsync(request, cancellationToken);
        return Ok(new ApiResult<AuthToken>
        {
            Data = token,
            Message = "Вход выполнен успешно",
            StatusCode = HttpStatusCode.OK,
            Success = true,
            DateTime = _timeProvider.GetUtcNow().UtcDateTime
        });
    }
}

using EventsAPI.Application.DTOs;

namespace EventsAPI.Application.Services;

public interface IAuthService
{
    Task RegisterAsync(RegisterUser request, CancellationToken cancellationToken = default);
    Task<AuthToken> LoginAsync(LoginUser request, CancellationToken cancellationToken = default);
}

namespace EventsAPI.Application.DTOs;

/// <summary>JWT-токен аутентифицированного пользователя.</summary>
public class AuthToken
{
    public required string Token { get; set; }
}

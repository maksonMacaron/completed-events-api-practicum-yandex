using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventsAPI.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Идентификатор пользователя отсутствует в токене");
    }
}

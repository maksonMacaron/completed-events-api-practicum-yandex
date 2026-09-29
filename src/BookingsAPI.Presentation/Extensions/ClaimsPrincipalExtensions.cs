using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BookingsAPI.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Токен не содержит идентификатор пользователя");
    }
}

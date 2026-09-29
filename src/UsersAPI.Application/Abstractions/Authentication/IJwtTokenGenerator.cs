using UsersAPI.Domain.Entities;

namespace UsersAPI.Application.Abstractions.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}

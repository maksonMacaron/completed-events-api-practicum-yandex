using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Abstractions.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}

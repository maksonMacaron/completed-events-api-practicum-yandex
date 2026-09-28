using EventsAPI.Domain.Entities;

namespace EventsAPI.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default);
}

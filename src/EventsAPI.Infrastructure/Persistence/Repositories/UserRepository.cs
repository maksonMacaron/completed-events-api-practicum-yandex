using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<User?> GetByLoginAsync(
        string login,
        CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Login == login, cancellationToken);

    public Task<bool> ExistsByLoginAsync(
        string login,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Login == login, cancellationToken);
}

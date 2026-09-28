using EventsAPI.Domain.Entities;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventsAPI.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;

    public UserRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_SavesUser()
    {
        await _fixture.ResetDatabaseAsync();
        var user = CreateUser("reader");

        await using (var context = _fixture.CreateContext())
            await new UserRepository(context).AddAsync(user);

        await using var assertContext = _fixture.CreateContext();
        var saved = await new UserRepository(assertContext).GetByLoginAsync(user.Login);
        Assert.NotNull(saved);
        Assert.Equal(user.PasswordHash, saved.PasswordHash);
        Assert.Equal(UserRole.User, saved.Role);
    }

    [Fact]
    public async Task AddAsync_DuplicateLogin_Throws()
    {
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateContext();
        var repository = new UserRepository(context);
        await repository.AddAsync(CreateUser("duplicate"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(CreateUser("duplicate")));
    }

    private static User CreateUser(string login) =>
        new(login, new string('A', 64), UserRole.User);
}

using EventsAPI.Application.Abstractions.Authentication;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.DTOs;
using EventsAPI.Application.Services;
using EventsAPI.Domain.Entities;
using EventsAPI.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly ServiceProvider _provider = TestServices.BuildProvider();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task RegisterAsync_ValidRequest_SavesPasswordHash()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await service.RegisterAsync(new RegisterUser
        {
            Login = "reader",
            Password = "password",
            Role = UserRole.User
        });

        var user = await repository.GetByLoginAsync("reader");
        Assert.NotNull(user);
        Assert.NotEqual("password", user.PasswordHash);
        Assert.True(passwordHasher.Verify("password", user.PasswordHash));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var request = new RegisterUser
        {
            Login = "admin",
            Password = "secret",
            Role = UserRole.Admin
        };
        await service.RegisterAsync(request);

        var result = await service.LoginAsync(new LoginUser
        {
            Login = request.Login,
            Password = request.Password
        });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task LoginAsync_UnknownLoginAndWrongPassword_ReturnSameError()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await service.RegisterAsync(new RegisterUser
        {
            Login = "member",
            Password = "correct",
            Role = UserRole.User
        });

        var unknownUserError = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(new LoginUser
            {
                Login = "unknown",
                Password = "correct"
            }));
        var wrongPasswordError = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(new LoginUser
            {
                Login = "member",
                Password = "wrong"
            }));

        Assert.Equal(unknownUserError.Message, wrongPasswordError.Message);
    }
}

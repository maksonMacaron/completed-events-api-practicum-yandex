using UsersAPI.Application.Abstractions.Authentication;
using UsersAPI.Application.Abstractions.Persistence;
using UsersAPI.Application.DTOs;
using UsersAPI.Domain.Entities;
using UsersAPI.Domain.Exceptions;

namespace UsersAPI.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task RegisterAsync(
        RegisterUser request,
        CancellationToken cancellationToken = default)
    {
        if (await _userRepository.ExistsByLoginAsync(request.Login, cancellationToken))
            throw new UserAlreadyExistsException();

        var user = new User(
            request.Login,
            _passwordHasher.Hash(request.Password),
            request.Role);

        await _userRepository.AddAsync(user, cancellationToken);
    }

    public async Task<AuthToken> LoginAsync(
        LoginUser request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByLoginAsync(request.Login, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return new AuthToken { Token = _tokenGenerator.GenerateToken(user) };
    }
}

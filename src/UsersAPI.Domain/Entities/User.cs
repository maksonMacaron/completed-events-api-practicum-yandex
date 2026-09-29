using UsersAPI.Domain.Exceptions;

namespace UsersAPI.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Login { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }

    private User()
    {
        Login = null!;
        PasswordHash = null!;
    }

    public User(string login, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new DomainValidationException("Логин обязателен для заполнения");

        if (login.Length is < 3 or > 100)
            throw new DomainValidationException("Логин должен содержать от 3 до 100 символов");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException("Хеш пароля обязателен для заполнения");

        if (!Enum.IsDefined(role))
            throw new DomainValidationException("Указана неизвестная роль пользователя");

        Id = Guid.NewGuid();
        Login = login;
        PasswordHash = passwordHash;
        Role = role;
    }
}

public enum UserRole
{
    User,
    Admin
}

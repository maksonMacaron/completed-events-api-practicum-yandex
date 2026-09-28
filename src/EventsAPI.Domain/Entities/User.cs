using EventsAPI.Domain.Exceptions;

namespace EventsAPI.Domain.Entities;

/// <summary>Пользователь приложения.</summary>
public class User
{
    /// <summary>Уникальный идентификатор пользователя.</summary>
    public Guid Id { get; private set; }

    /// <summary>Логин пользователя.</summary>
    public string Login { get; private set; }

    /// <summary>Хеш пароля пользователя.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Роль пользователя.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Бронирования пользователя.</summary>
    public ICollection<Booking> Bookings { get; private set; } = [];

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

/// <summary>Роль пользователя в приложении.</summary>
public enum UserRole
{
    User,
    Admin
}

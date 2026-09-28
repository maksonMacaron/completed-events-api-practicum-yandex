namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка регистрации пользователя с занятым логином.</summary>
public sealed class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException()
        : base("Пользователь с таким логином уже существует")
    {
    }
}

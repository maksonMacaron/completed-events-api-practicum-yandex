namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка проверки учётных данных пользователя.</summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Неверный логин или пароль")
    {
    }
}

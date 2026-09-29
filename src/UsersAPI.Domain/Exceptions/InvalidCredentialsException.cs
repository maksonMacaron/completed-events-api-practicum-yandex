namespace UsersAPI.Domain.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Неверный логин или пароль")
    {
    }
}

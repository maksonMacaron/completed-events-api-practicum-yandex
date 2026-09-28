namespace UsersAPI.Domain.Exceptions;

public sealed class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException() : base("Пользователь с таким логином уже существует")
    {
    }
}

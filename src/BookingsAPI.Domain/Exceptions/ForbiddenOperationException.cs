namespace BookingsAPI.Domain.Exceptions;

public sealed class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException() : base("Недостаточно прав для выполнения операции")
    {
    }
}

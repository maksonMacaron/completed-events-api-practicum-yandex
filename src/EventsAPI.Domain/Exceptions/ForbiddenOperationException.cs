namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка выполнения операции без необходимых прав.</summary>
public sealed class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException()
        : base("Недостаточно прав для выполнения операции")
    {
    }
}

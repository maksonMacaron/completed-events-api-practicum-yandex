namespace EventsAPI.Domain.Exceptions;

/// <summary>Ошибка нарушения правил предметной области.</summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }
}

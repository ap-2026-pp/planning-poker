namespace PlanningPoker.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with id '{key}' was not found.")
    {
    }

    public NotFoundException(string resourceName, string fieldName, object value)
        : base($"{resourceName} with {fieldName} '{value}' was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}

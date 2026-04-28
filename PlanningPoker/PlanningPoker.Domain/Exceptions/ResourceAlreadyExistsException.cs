namespace PlanningPoker.Domain.Exceptions;

public class ResourceAlreadyExistsException: Exception
{
    public ResourceAlreadyExistsException(string resourceName, object key)
        : base($"{resourceName} with name '{key}' already exists.")
    {
    }

    public ResourceAlreadyExistsException(string resourceName, string fieldName, object value)
        : base($"{resourceName} with {fieldName} '{value}' already exists.")
    {
    }

    public ResourceAlreadyExistsException(string message)
        : base(message)
    {
    }
}
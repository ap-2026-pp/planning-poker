namespace PlanningPoker.Domain.Exceptions;

public class ResourceAlreadyExistsException: Exception
{
    public ResourceAlreadyExistsException(string resourceName, object key)
        : base($"{resourceName} with name '{key}' already exists.")
    {
    }
}
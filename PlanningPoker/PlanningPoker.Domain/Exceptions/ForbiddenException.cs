namespace PlanningPoker.Domain.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string action, string resourceName)
        : base($"You do not have permission to {action} this {resourceName}.")
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }
}

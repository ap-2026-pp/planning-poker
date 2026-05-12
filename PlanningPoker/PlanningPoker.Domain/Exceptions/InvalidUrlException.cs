namespace PlanningPoker.Domain.Exceptions;

public class InvalidUrlException : Exception
{
    public InvalidUrlException(string message)
        : base(message)
    {
    }
}
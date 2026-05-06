namespace PlanningPoker.Domain.Exceptions;

public class InvalidUrlException : Exception
{
    public InvalidUrlException()
        : base("The provided URL is invalid.")
    {
    }

    public InvalidUrlException(string message)
        : base(message)
    {
    }

    public InvalidUrlException(string url, string reason)
        : base($"Invalid URL '{url}'. Reason: {reason}")
    {
    }
}
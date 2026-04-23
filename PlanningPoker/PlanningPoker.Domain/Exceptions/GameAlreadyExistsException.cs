namespace PlanningPoker.Domain.Exceptions;

public class GameAlreadyExistsException(string name) : Exception($"Game with name \'{name}\' already exists")
{
    
}
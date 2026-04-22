namespace PlanningPoker.Domain.Exceptions;

public class NotFoundException(int id) : Exception($"Resource with id {id} not found")
{
    
}
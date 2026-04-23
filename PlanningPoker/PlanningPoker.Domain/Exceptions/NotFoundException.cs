namespace PlanningPoker.Domain.Exceptions;

public class NotFoundException(Guid id) : Exception($"Resource with id {id} not found")
{
    
}
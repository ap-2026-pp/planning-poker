using PlanningPoker.PlanningPoker.DAL;
namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface IBaseRepository<T> where T : class<T>
{
    Task<T?> GetByIdAsync(Guid id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task SaveChangesAsync();
}

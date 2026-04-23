using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;

namespace PlanningPoker.DAL.Repositories;


public class BaseRepository<T>(AppDbContext context, DbSet<T> dbSet) : IBaseRepository<T> where T : class
{
    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await dbSet.FindAsync(id);
    }

    public async Task AddAsync(T entity)
    { 
        await dbSet.AddAsync(entity);
    }
    public void Update(T entity)
    {
        dbSet.Update(entity);
    }
    
    public void Delete(T entity)
    {
        dbSet.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
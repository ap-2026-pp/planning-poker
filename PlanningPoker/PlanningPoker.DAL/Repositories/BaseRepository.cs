using PlanningPoker.Domain.Models;
using Microsoft.EntityFrameworkCore;
namespace PlanningPoker.Domain.Interfaces.Repositories;


public class BaseRepository<T> where T : class IBaseRepository<T> 
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;
    
    public BaseRepository(AppDbContext contex) 
    {
        _context = context;
        _dbSet = _context.Set<T>();

    }
    
    public Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public Task AddAsync(T entity)
    {
        return await _dbSet.AddAsync(entity);
    }
    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }
    
    public void Delete(T entity)
    {
        _dbset.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync()
    }
}
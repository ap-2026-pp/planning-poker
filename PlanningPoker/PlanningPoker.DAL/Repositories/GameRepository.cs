using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

public class GameRepository(AppDbContext context) : BaseRepository<Game>(context), IGameRepository
{
    public async Task<bool> ExistsByNameAsync(string name, Guid createdBy)
    {
        return await DbSet.AnyAsync(g => g.Name == name && g.CreatedBy == createdBy);
    }
}

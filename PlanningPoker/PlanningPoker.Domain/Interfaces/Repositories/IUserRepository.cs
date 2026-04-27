namespace PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> RefreshTokenAsync(string refreshToken);
}
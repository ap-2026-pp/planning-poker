using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

internal class TimerRepository(AppDbContext context) : BaseRepository<GameTimer>(context),ITimerRepository
{
}
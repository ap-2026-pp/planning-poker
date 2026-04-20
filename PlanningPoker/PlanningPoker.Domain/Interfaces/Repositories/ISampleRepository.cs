using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Repositories;

public interface ISampleRepository
{
    Task<SampleModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SampleModel>> ListAsync(CancellationToken ct = default);
    Task AddAsync(SampleModel entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
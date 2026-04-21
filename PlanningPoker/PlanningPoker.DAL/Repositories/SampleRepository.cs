using Microsoft.EntityFrameworkCore;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.DAL.Repositories;

 public class SampleRepository : ISampleRepository
{
//     private readonly AppDbContext _db;

//     public SampleRepository(AppDbContext db) => _db = db;

//     public Task<SampleModel?> GetByIdAsync(int id, CancellationToken ct = default) =>
//         _db.SampleEntities.FirstOrDefaultAsync(e => e.Id == id, ct);

//     public async Task<IReadOnlyList<SampleModel>> ListAsync(CancellationToken ct = default) =>
//         await _db.SampleEntities.AsNoTracking().ToListAsync(ct);

//     public async Task AddAsync(SampleModel entity, CancellationToken ct = default) =>
//         await _db.SampleEntities.AddAsync(entity, ct);

//     public Task SaveChangesAsync(CancellationToken ct = default) =>
//         _db.SaveChangesAsync(ct);
}
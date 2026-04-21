using PlanningPoker.Domain.DTOs;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;

namespace PlanningPoker.BLL.Services;

 public class SampleService : ISampleService
{
//     private readonly ISampleRepository _repo;

//     public SampleService(ISampleRepository repo) => _repo = repo;

//     public async Task<SampleDto> CreateAsync(CreateSampleDto dto, CancellationToken ct = default)
//     {
//         var entity = SampleMapper.ToEntity(dto);
//         await _repo.AddAsync(entity, ct);
//         await _repo.SaveChangesAsync(ct);
//         return SampleMapper.ToDto(entity);
//     }

//     public async Task<SampleDto?> GetAsync(int id, CancellationToken ct = default)
//     {
//         var entity = await _repo.GetByIdAsync(id, ct);
//         return entity is null ? null : SampleMapper.ToDto(entity);
//     }

//     public async Task<IReadOnlyList<SampleDto>> ListAsync(CancellationToken ct = default)
//     {
//         var list = await _repo.ListAsync(ct);
//         return list.Select(SampleMapper.ToDto).ToList();
//     }
}
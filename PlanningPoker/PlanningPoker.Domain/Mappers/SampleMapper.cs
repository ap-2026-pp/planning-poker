
using PlanningPoker.Domain.DTOs;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class SampleMapper
{
    public static SampleDto ToDto(SampleModel e) =>
        new()
        {
            Id = e.Id,
            Name = e.Name,
            CreatedAt = e.CreatedAt
        };

    public static SampleModel ToEntity(CreateSampleDto dto) =>
        new()
        {
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow
        };
}
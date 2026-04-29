namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Plane;
public interface IPlaneService
{
    Task<List<PlaneIssueDto>> GetIssuesAsync(ImportPlaneIssuesDto dto);
}


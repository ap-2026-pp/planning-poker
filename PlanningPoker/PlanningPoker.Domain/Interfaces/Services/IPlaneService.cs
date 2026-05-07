using PlanningPoker.BLL.DTOs.Plane;
namespace PlanningPoker.Domain.Interfaces.Services;

public interface IPlaneService
{
    Task<List<PlaneIssueDto>> GetIssuesAsync(Guid gameId, string workspaceSlug, string projectId, string apiKey);
}

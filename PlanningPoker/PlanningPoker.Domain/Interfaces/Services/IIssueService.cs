namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;

public interface IIssueService
{
    Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId);
    Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId);
    Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto);
    Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto);
    Task DeleteIssueAsync(Guid gameId, Guid issueId);
    Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto);
    Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto);
    Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId);
}

namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;

public interface IIssueService
{
    Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId, Guid userId);
    Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId, Guid userId);
    Task<IssueDto> CreateIssueAsync(Guid gameId, Guid userId, CreateIssueDto dto);
    Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, Guid userId, UpdateIssueDto dto);
    Task DeleteIssueAsync(Guid gameId, Guid issueId, Guid userId);
    Task ReorderIssuesAsync(Guid gameId, Guid userId, ReorderIssueDto dto);
    Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, Guid userId, ImportPlaneIssuesDto dto);
    Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId, Guid userId);
}

using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Issue.Export;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Models;
namespace PlanningPoker.Domain.Interfaces.Services;

public interface IIssueService
{
    Task<IEnumerable<IssueDto>> GetIssuesByGameAsync(Guid gameId);
    Task<IssueDetailsDto> GetIssueByIdAsync(Guid gameId, Guid issueId);
    Task<IssueDto> CreateIssueAsync(Guid gameId, CreateIssueDto dto);
    Task<IssueDto> UpdateIssueAsync(Guid gameId, Guid issueId, UpdateIssueDto dto);
    Task<IssueDto> SetIssueActiveAsync(Guid gameId, Guid issueId);
    Task DeleteIssueAsync(Guid gameId, Guid issueId);
    Task DeleteAllIssuesAsync(Guid gameId);
    Task ReorderIssuesAsync(Guid gameId, ReorderIssueDto dto);
    Task<IEnumerable<IssueDto>> ImportIssueByPlaneAsync(Guid gameId, ImportPlaneIssuesDto dto);
    Task<ExportIssuesFileDto> ExportToCsvAsync(Guid gameId, ExportIssuesRequestDto dto);
    Task<Issue> GetAndValidateActiveIssueAsync(Guid gameId, Guid issueId);
}
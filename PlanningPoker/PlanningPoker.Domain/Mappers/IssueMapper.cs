using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class IssueMapper
{
    public static IssueDto ToDto(Issue issue)
    {
        return new IssueDto
        {
            Id = issue.Id,
            Code = issue.Code,
            Url = issue.Url,
            Title = issue.Title,
            Description = issue.Description,
            Order = issue.Order,
            IsCurrent = issue.IsCurrent,
            IsRemoved = issue.IsRemoved,
        };
    }

    public static IssueDetailsDto ToDetailsDto(Issue issue)
    {
        return new IssueDetailsDto
        {
            Id = issue.Id,
            Code = issue.Code,
            Url = issue.Url,
            Title = issue.Title,
            Description = issue.Description,
            IsCurrent = issue.IsCurrent
        };
    }
    public static Issue ToEntity(
        CreateIssueDto dto,
        Guid gameId,
        Guid createdByParticipantId,
        int order
    )
    {
        return new Issue
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Url = string.Empty,
            Title = dto.Title,
            Description = string.Empty,
            Order = order,
            IsCurrent = false,
            IsRemoved = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByParticipantId
    };
    }

    public static void UpdateEntity(Issue issue, UpdateIssueDto dto)
    {
        issue.Url = dto.Url ?? string.Empty;
        issue.Title = dto.Title;
        issue.Description = dto.Description ?? string.Empty;
    }
}
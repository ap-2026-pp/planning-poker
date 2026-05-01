using System.Data.Common;
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
            IsCurrent = issue.IsCurrent
        };
    }

    public static IssueDetailsDto ToDetailsDto(Issue issue)
    {
        return new IssueDetailsDto
        {
            Id = issue.Id,
            Code = issue.Code,
            Title = issue.Title,
            Description = issue.Description,
            IsCurrent = issue.IsCurrent
        };
    }
}
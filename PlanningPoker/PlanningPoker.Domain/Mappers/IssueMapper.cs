using System.Net;
using Microsoft.EntityFrameworkCore.Query;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Mappers;

public static class IssueMapper
{
    public static IssueDto ToDto(Issue issue, VotingResult? result = null)
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
            FinalEstimate = result?.FinalEstimate,
            Status = result != null
                ? IssueStatus.Completed
                : (issue.IsCurrent ? IssueStatus.Voting : IssueStatus.Pending),
            IsRemoved = issue.IsRemoved,
        };
    }

    public static IssueDetailsDto ToDetailsDto(Issue issue, VotingResult? result)
    {
        return new IssueDetailsDto
        {
            Id = issue.Id,
            Code = issue.Code,
            Url = issue.Url,
            Title = issue.Title,
            Description = issue.Description,
            FinalEstimate = result.FinalEstimate,
            Status = result != null
            ? IssueStatus.Completed
            : (issue.IsCurrent ? IssueStatus.Voting : IssueStatus.Pending),
            IsCurrent = issue.IsCurrent,
        };
    }
}
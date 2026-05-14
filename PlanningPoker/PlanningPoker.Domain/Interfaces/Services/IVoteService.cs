using PlanningPoker.Domain.DTOs.Vote;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IVoteService
{
    public Task<VoteDto> CreateVoteByGameIdAsync(Guid gameId, Guid issueId, CreateVoteDto dto);
    public Task DeleteVoteAsync(Guid gameId, Guid issueId);
}
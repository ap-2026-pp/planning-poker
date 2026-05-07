namespace PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.DTOs.VotingHistory;

public interface IVotingHistoryService
{
    public Task<VotingHistoryListDto> GetHistoryAsync(Guid gameId, VotingHistoryQueryDto query);
    public Task<VotingHistoryDetailsDto> GetHistoryDetailsAsync(Guid gameId, Guid entryId);
    public Task<byte[]> ExportHistoryCsvAsync(Guid gameId, ExportVotingHistoryDto dto);
}
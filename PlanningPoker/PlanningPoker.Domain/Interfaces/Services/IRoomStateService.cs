using PlanningPoker.Domain.DTOs.Room;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.Interfaces.Services;

public interface IRoomStateService
{
    public Task<RoomStateDto> GetRoomStateAsync(Guid gameId);
    public List<string> GetAvailableCards(Game game);
    public Task<RoomStateDto> RevealCardsAsync(Guid gameId);
    public Task<RoomStateDto> ResetRoundAsync(Guid gameId);
    public Task<RoundResultDto> GetFinalEstimateAsync(Guid gameId, Guid issueId);
    public Task<(string FinalEstimate, double? Average, double? Agreement)> CalculateSystemEstimate(List<Vote> votes);
}


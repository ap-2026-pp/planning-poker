using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameRequestDto
{
    public string Name { get; set; }
    public VotingSystem VotingSystem { get; set; }
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<GameParticipantDto> Participants { get; set; }
    
    // TODO remove when auth implemented
    public Guid? CreatedBy { get; set; }
}
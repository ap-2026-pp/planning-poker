using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameDto
{
    public string Name { get; set; }
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    
    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool IsActive { get; set; } = true;
    
    // TODO remove when auth implemented
    public Guid? CreatedBy { get; set; }
}

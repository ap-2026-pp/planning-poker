using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class GameDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    public bool AutoRevealCards { get; set; } = true;
    public bool ShowAverage { get; set; } = true;
    public bool ShowCountdownAnimation { get; set; } = true;
    public bool IsActive { get; set; } = true;
    
    // TODO remove when auth implemented
    public Guid? CreatedBy { get; set; }
}

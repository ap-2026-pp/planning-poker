using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class CreateGameRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    
    [MaxLength(200)]
    public string? HostDisplayName { get; set; }
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    public RevealPolicy RevealPolicy { get; set; } = RevealPolicy.MasterOnly;
    public IssuesPolicy IssuesPolicy { get; set; } = IssuesPolicy.MasterOnly;
    public bool AutoRevealCards { get; set; } = true;
    public bool ShowAverage { get; set; } = true;
    public bool ShowCountdownAnimation { get; set; } = true;
    public bool EnableFunFeatures { get; set; }
}

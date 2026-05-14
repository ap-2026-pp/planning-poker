using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class CreateGameRequestDto
{
    [Required]
    [MaxLength(200)]
    public string? DisplayName { get; set; } 
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;
    public string? CustomValues { get; set; }
    public RevealPolicy RevealPolicy { get; set; } = RevealPolicy.MasterOnly;
    public IssuesPolicy IssuesPolicy { get; set; } = IssuesPolicy.MasterOnly;
    public bool AutoResetTimer { get; set; } = true;
    public bool AutoRevealCards { get; set; } = true;
    public bool ShowAverage { get; set; } = true;
    public bool ShowCountdownAnimation { get; set; } = true;
    public bool EnableFunFeatures { get; set; }
    public int DefaultTimerMinutes { get; set; } = 1;
}

using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;

public class UpdateGameRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;

    [MaxLength(200)]
    [RegularExpression(@"^[^,]+(,[^,]+)*$",
     ErrorMessage = "Значення мають бути розділені комами без зайвих символів")]
    public string? CustomValues { get; set; }
    public RevealPolicy RevealPolicy { get; set; } = RevealPolicy.MasterOnly;
    public IssuesPolicy IssuesPolicy { get; set; } = IssuesPolicy.MasterOnly;
    public bool AutoRevealCards { get; set; } = true;
    public bool EnableFunFeatures { get; set; }
    public bool ShowAverage { get; set; } = true;
    public bool ShowCountdownAnimation { get; set; } = true;
    public bool IsActive { get; set; }
    public int DefaultTimerMinutes { get; set; }
    public bool AutoResetTimer { get; set; }
}

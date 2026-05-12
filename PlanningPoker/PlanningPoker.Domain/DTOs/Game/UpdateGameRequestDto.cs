using System.ComponentModel.DataAnnotations;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.Domain.DTOs.Game;
/// <summary>
/// EnableFunFeatures
/// перемикач "Enable fun features"
/// 
/// AutoResetTimer
/// перемикач "Time issues"
/// </summary>
public class UpdateGameRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    public VotingSystem VotingSystem { get; set; } = VotingSystem.Custom;

    [MaxLength(200)]
    [RegularExpression(@"^[^,]+(,[^,]+)*$",
     ErrorMessage = "Значення мають бути розділені комами без зайвих символів")]
    public string? CustomValues { get; set; }
    public RevealPolicy RevealPolicy { get; set; }
    public IssuesPolicy IssuesPolicy { get; set; }
    public bool AutoRevealCards { get; set; } = true;
    public bool EnableFunFeatures { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }
    public bool AutoResetTimer { get; set; } 
    public int DefaultTimerMinutes { get; set; }
    
    public bool IsActive { get; set; }
}

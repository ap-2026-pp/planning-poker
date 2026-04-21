namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;
public class Game
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public string VotingSystem { get; set; }

    [Required]
    [MaxLength(20)]
    public string InviteCode { get; set; }

    public bool AutoRevealCards { get; set; }
    public bool ShowAverage { get; set; }
    public bool ShowCountdownAnimation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; }
    public ICollection<GameParticipant> Participants { get; set; }
    public ICollection<Issue> Issues { get; set; }
    public ICollection<Vote> Votes { get; set; }
    public ICollection<VotingHistoryEntry> HistoryEntries { get; set; }
}
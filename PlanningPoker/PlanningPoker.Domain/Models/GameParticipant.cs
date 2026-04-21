namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;

public class GameParticipant
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; }

    [Required]
    public SessionRole SessionRole { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; }

    public Game Game { get; set; }
    public AppUser User { get; set; }
    public ICollection<Vote> Votes { get; set; }
}
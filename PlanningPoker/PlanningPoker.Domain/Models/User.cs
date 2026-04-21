namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
public class User : IdentityUser<Guid>
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Game> CreatedGames { get; set; }
    public ICollection<GameParticipant> Participants { get; set; }
}


namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string RefreshToken { get; set; }
    public ICollection<Game> CreatedGames { get; set; } = new List<Game>();
    public ICollection<GameParticipant> Participants { get; set; } = new List<GameParticipant>();
}

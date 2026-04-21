namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;

public class Issue
{
    public int Id { get; set; }

    public int GameId { get; set; }

    [MaxLength(50)]
    public string Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }
    public string Description { get; set; }

    public int Order { get; set; }
    public string FinalEstimate { get; set; }

    public bool IsCurrent { get; set; }

    public DateTime CreatedAt { get; set; }
    public Game Game { get; set; }
    public ICollection<Vote> Votes { get; set; }
}
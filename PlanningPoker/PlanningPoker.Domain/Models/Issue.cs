namespace PlanningPoker.Domain.Models;
using System.ComponentModel.DataAnnotations;

public class Issue
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int Order { get; set; }
    public string FinalEstimate { get; set; }
    public bool IsCurrent { get; set; }
    public bool isRemoved{ get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Vote> Votes { get; set; }
}


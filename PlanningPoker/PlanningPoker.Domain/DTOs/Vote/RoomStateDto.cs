namespace PlanningPoker.Domain.DTOs.Vote;
using  PlanningPoker.BLL.DTOs.Issue;
/// <summary>
/// Голос одного учасника в конкретному раунді
/// </summary>
public class RoomStateDto
{
    public Guid GameId { get; set; }
    public IssueDto? ActiveIssue { get; set; }
    public bool IsRevealed { get; set; }
    public string VotingSystem { get; set; } = null!;
    public List<string> AvailableCards { get; set; } = new();
    
    public int VotedCount { get; set; }
    public int TotalPlayers { get; set; }

    public string? MyVote { get; set; }
    public bool CanVote { get; set; }
    public bool CanReveal { get; set; }
    public bool CanManage { get; set; } 
    public List<ParticipantVoteStatusDto> Participants { get; set; } = new();
    public RoundResultDto? Result { get; set; }
}


using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.Domain.DTOs.Timer; 
using PlanningPoker.Domain.DTOs.Vote;

namespace PlanningPoker.Domain.DTOs.Room;

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

    public TimerDto? Timer { get; set; } 
    
    public bool CanManageTimer { get; set; }
    public bool AutoRevealEnabled { get; set; }

    public List<ParticipantVoteStatusDto> Participants { get; set; } = new();
    public RoundResultDto? Result { get; set; }
}
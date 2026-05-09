namespace PlanningPoker.API.Hubs;

public static class GameRoomHubEvents
{
    public const string ParticipantJoined = "ParticipantJoined";
    public const string ParticipantLeft = "ParticipantLeft";
    public const string ParticipantKicked = "ParticipantKicked";
    public const string ParticipantUpdated = "ParticipantUpdated";
    public const string GameUpdated = "GameUpdated";
    public const string IssueCreated = "IssueCreated";
}

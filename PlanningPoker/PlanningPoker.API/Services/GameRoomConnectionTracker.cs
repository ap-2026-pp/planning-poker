namespace PlanningPoker.API.Services;

public class GameRoomConnectionTracker
{
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, HashSet<string>> _connectionsByParticipant = [];
    private readonly Dictionary<string, Guid> _participantsByConnection = [];

    public bool RegisterConnection(Guid participantId, string connectionId)
    {
        lock (_sync)
        {
            if (_participantsByConnection.TryGetValue(connectionId, out var existingParticipantId))
            {
                RemoveConnectionInternal(existingParticipantId, connectionId);
            }

            _participantsByConnection[connectionId] = participantId;

            if (!_connectionsByParticipant.TryGetValue(participantId, out var participantConnections))
            {
                participantConnections = [];
                _connectionsByParticipant[participantId] = participantConnections;
            }

            var isFirstConnection = participantConnections.Count == 0;
            participantConnections.Add(connectionId);

            return isFirstConnection;
        }
    }

    public GameRoomConnectionStateChange? UnregisterConnection(string connectionId)
    {
        lock (_sync)
        {
            if (!_participantsByConnection.Remove(connectionId, out var participantId))
            {
                return null;
            }

            if (!_connectionsByParticipant.TryGetValue(participantId, out var participantConnections))
            {
                return new GameRoomConnectionStateChange(participantId, true);
            }

            participantConnections.Remove(connectionId);
            var isLastConnection = participantConnections.Count == 0;

            if (isLastConnection)
            {
                _connectionsByParticipant.Remove(participantId);
            }

            return new GameRoomConnectionStateChange(participantId, isLastConnection);
        }
    }

    private void RemoveConnectionInternal(Guid participantId, string connectionId)
    {
        _participantsByConnection.Remove(connectionId);

        if (!_connectionsByParticipant.TryGetValue(participantId, out var participantConnections))
        {
            return;
        }

        participantConnections.Remove(connectionId);

        if (participantConnections.Count == 0)
        {
            _connectionsByParticipant.Remove(participantId);
        }
    }
}

public readonly record struct GameRoomConnectionStateChange(Guid ParticipantId, bool IsLastConnection);

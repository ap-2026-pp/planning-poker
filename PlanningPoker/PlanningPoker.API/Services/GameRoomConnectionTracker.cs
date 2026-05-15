using Microsoft.Extensions.Logging;

namespace PlanningPoker.API.Services;

public class GameRoomConnectionTracker(ILogger<GameRoomConnectionTracker> logger)
{
    // Covers the default SignalR automatic reconnect window and prevents transient drops
    // from immediately flipping a participant to offline.
    private static readonly TimeSpan OfflineGracePeriod = TimeSpan.FromSeconds(45);
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, HashSet<string>> _connectionsByParticipant = [];
    private readonly Dictionary<string, Guid> _participantsByConnection = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _pendingOfflineTransitions = [];

    public bool RegisterConnection(Guid participantId, string connectionId)
    {
        CancellationTokenSource? pendingOfflineTransition = null;
        bool isFirstConnection;

        lock (_sync)
        {
            if (_pendingOfflineTransitions.Remove(participantId, out var existingPendingOfflineTransition))
            {
                pendingOfflineTransition = existingPendingOfflineTransition;
            }

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

            isFirstConnection = participantConnections.Count == 0;
            participantConnections.Add(connectionId);
        }

        pendingOfflineTransition?.Cancel();
        return isFirstConnection;
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

    public void ScheduleOfflineTransition(Guid participantId, Func<Guid, Task> onOfflineTransition)
    {
        CancellationTokenSource pendingOfflineTransition;

        lock (_sync)
        {
            if (_connectionsByParticipant.TryGetValue(participantId, out var participantConnections) &&
                participantConnections.Count > 0)
            {
                return;
            }

            if (_pendingOfflineTransitions.TryGetValue(participantId, out var existingPendingOfflineTransition))
            {
                existingPendingOfflineTransition.Cancel();
            }

            pendingOfflineTransition = new CancellationTokenSource();
            _pendingOfflineTransitions[participantId] = pendingOfflineTransition;
        }

        _ = CompleteOfflineTransitionAsync(participantId, pendingOfflineTransition, onOfflineTransition);
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

    private async Task CompleteOfflineTransitionAsync(
        Guid participantId,
        CancellationTokenSource pendingOfflineTransition,
        Func<Guid, Task> onOfflineTransition)
    {
        try
        {
            await Task.Delay(OfflineGracePeriod, pendingOfflineTransition.Token);

            var shouldMarkParticipantOffline = false;

            lock (_sync)
            {
                var hasActiveConnections =
                    _connectionsByParticipant.TryGetValue(participantId, out var participantConnections) &&
                    participantConnections.Count > 0;

                if (!hasActiveConnections &&
                    _pendingOfflineTransitions.TryGetValue(participantId, out var currentPendingOfflineTransition) &&
                    ReferenceEquals(currentPendingOfflineTransition, pendingOfflineTransition))
                {
                    _pendingOfflineTransitions.Remove(participantId);
                    shouldMarkParticipantOffline = true;
                }
            }

            if (!shouldMarkParticipantOffline)
            {
                return;
            }

            await onOfflineTransition(participantId);
        }
        catch (OperationCanceledException)
        {
            // Participant reconnected before the grace period expired.
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to complete offline transition for participant {ParticipantId}.",
                participantId);
        }
        finally
        {
            pendingOfflineTransition.Dispose();
        }
    }
}

public readonly record struct GameRoomConnectionStateChange(Guid ParticipantId, bool IsLastConnection);

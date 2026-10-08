namespace Rechaos.Core.GameModel;

public enum GameNotificationKind : byte
{
    Information,
    CommandResult,
    Economy,
    Hire,
    Combat,
    Elimination,
    Objective,
    Research,
    Influence,
    Equipment,
    Movement,
    Control,
    Chaos,
    Crackdown,
    Police,
    ControlLost,
    HireInsufficientCash,
    HireSectorFull,
    HireGangLimit
}

/// <summary>A mechanical notification reference; presentation supplies localized text.</summary>
public sealed record GameNotification(
    long Sequence,
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    GameNotificationKind Kind,
    GangId? Gang = null,
    int? SectorId = null,
    long? RelatedEventSequence = null);

internal static class GameNotificationValidator
{
    public static bool IsValidHistory(
        IReadOnlyList<GameNotification> values,
        long nextSequence,
        int currentTurn,
        IReadOnlyList<GameEvent> events)
    {
        var eventsBySequence = events.ToDictionary(gameEvent => gameEvent.Sequence);
        if (nextSequence < 0) return false;
        // NotificationQueue.Enqueue goes past its capacity only while every notification it holds
        // belongs to the newest turn or the one before it, and it adds them in turn order, which its
        // drop loop relies on when the history is restored.
        if (values.Count > MatchLimits.NotificationsPerPlayer
            && values.Where((value, index) =>
                    value.Turn < values[^1].Turn - 1 || index > 0 && value.Turn < values[index - 1].Turn)
                .Any())
            return false;
        if (values.Count > 0 && values.Where((value, index) =>
                value.Sequence != nextSequence - values.Count + index).Any())
            return false;
        return values.All(value =>
            value.Turn is >= 1 && value.Turn <= currentTurn
            && Enum.IsDefined(value.Phase)
            && Enum.IsDefined(value.Kind)
            && (value.Phase == TurnPhase.Execution) == value.ExecutionPhase.HasValue
            && (value.ExecutionPhase is null || Enum.IsDefined(value.ExecutionPhase.Value))
            && value.SectorId is null or >= 0 and < MatchLimits.SectorCount
            && (value.RelatedEventSequence is null
                || eventsBySequence.TryGetValue(value.RelatedEventSequence.Value, out var gameEvent)
                && gameEvent.Turn == value.Turn
                && gameEvent.Phase == value.Phase
                && gameEvent.ExecutionPhase == value.ExecutionPhase));
    }
}

public sealed class NotificationQueue
{
    private readonly Queue<GameNotification> _items = [];

    public NotificationQueue(int capacity = MatchLimits.NotificationsPerPlayer)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public int Capacity { get; }
    public int Count => _items.Count;
    public IReadOnlyList<GameNotification> Items => _items.ToArray();

    /// <summary>
    /// Adds a notification, dropping the oldest ones while the queue is full. RULE-EVENT-002: the
    /// Last Turn reports are the first 32 of a resolution, taken from the notifications of the
    /// turn just completed, so only notifications of earlier turns are dropped. A turn in which a
    /// player gets more notifications than the capacity keeps all of them until the turn after
    /// next has added its own.
    /// </summary>
    public void Enqueue(GameNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        while (_items.Count >= Capacity && _items.Peek().Turn < notification.Turn - 1) _items.Dequeue();
        _items.Enqueue(notification);
    }

    public bool TryDequeue(out GameNotification? notification) => _items.TryDequeue(out notification);
    public void Clear() => _items.Clear();
}

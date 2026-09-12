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
        long nextEventSequence)
    {
        if (values.Count > MatchLimits.NotificationsPerPlayer || nextSequence < 0)
            return false;
        if (values.Count > 0 && values.Where((value, index) =>
                value.Sequence != nextSequence - values.Count + index).Any())
            return false;
        return values.All(value =>
            value.Turn >= 1
            && Enum.IsDefined(value.Phase)
            && Enum.IsDefined(value.Kind)
            && (value.Phase == TurnPhase.Execution) == value.ExecutionPhase.HasValue
            && (value.ExecutionPhase is null || Enum.IsDefined(value.ExecutionPhase.Value))
            && value.SectorId is null or >= 0 and < MatchLimits.SectorCount
            && (value.RelatedEventSequence is null
                || value.RelatedEventSequence >= 0
                && value.RelatedEventSequence < nextEventSequence));
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

    public void Enqueue(GameNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (_items.Count == Capacity) _items.Dequeue();
        _items.Enqueue(notification);
    }

    public bool TryDequeue(out GameNotification? notification) => _items.TryDequeue(out notification);
    public void Clear() => _items.Clear();
}

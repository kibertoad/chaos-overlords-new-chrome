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
    Equipment
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

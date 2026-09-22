namespace Rechaos.Core.GameModel;

public readonly record struct PlayerId
{
    public PlayerId(int value)
    {
        if (value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }
    public override string ToString() => Value.ToString();
}

public readonly record struct GangId
{
    public GangId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }
    public override string ToString() => Value.ToString();
}

public enum CommandTargetKind : byte
{
    None,
    Gang,
    Sector,
    Site,
    Item
}

public readonly record struct CommandTarget
{
    private CommandTarget(CommandTargetKind kind, int id)
    {
        Kind = kind;
        Id = id;
    }

    public CommandTargetKind Kind { get; }
    public int Id { get; }

    public static CommandTarget None => new(CommandTargetKind.None, -1);
    public static CommandTarget Gang(GangId id) => new(CommandTargetKind.Gang, id.Value);
    public static CommandTarget Sector(int id) => Create(CommandTargetKind.Sector, id, MatchLimits.SectorCount);
    public static CommandTarget Site(int id) => Create(CommandTargetKind.Site, id, MatchLimits.SiteCount);
    public static CommandTarget Item(int id) => Create(CommandTargetKind.Item, id, MatchLimits.ItemSlots);

    private static CommandTarget Create(CommandTargetKind kind, int id, int exclusiveMaximum)
    {
        if (id < 0 || id >= exclusiveMaximum) throw new ArgumentOutOfRangeException(nameof(id));
        return new CommandTarget(kind, id);
    }
}

/// <summary>Player or AI intent. Resolution emits events separately.</summary>
public sealed record GameCommand(
    PlayerId Player,
    GangId Gang,
    GangAction Action,
    CommandTarget Target,
    bool Repeat = false,
    CommandTarget? SecondaryTarget = null,
    CommandTarget? TertiaryTarget = null,
    CommandTarget? QuaternaryTarget = null)
{
    /// <summary>The one-to-three exact items selected by the sell panel.</summary>
    public IEnumerable<CommandTarget> SellTargets()
    {
        yield return Target;
        if (SecondaryTarget is { } secondary) yield return secondary;
        if (TertiaryTarget is { } tertiary) yield return tertiary;
    }

    /// <summary>The one-to-three exact items selected by the give panel.</summary>
    public IEnumerable<CommandTarget> GiveTargets()
    {
        if (SecondaryTarget is { } secondary) yield return secondary;
        if (TertiaryTarget is { } tertiary) yield return tertiary;
        if (QuaternaryTarget is { } quaternary) yield return quaternary;
    }
}

public sealed record QueuedCommand(long Sequence, GameCommand Command)
{
    public ExecutionPhase ExecutionPhase => TurnStructure.PhaseFor(Command.Action);
}

/// <summary>
/// Deterministic command storage for a turn. Replacement receives a new sequence
/// number to preserve submission chronology in replays and persisted state.
/// Each mutating phase resolver applies its recovered action-specific
/// player/roster ordering before it consumes RNG or changes match state.
/// </summary>
public sealed class TurnCommandQueue
{
    private readonly Dictionary<GangId, QueuedCommand> _byGang = [];
    private long _nextSequence;
    private IReadOnlyList<QueuedCommand>? _executionPlan;

    public int Count => _byGang.Count;
    internal long NextSequence => _nextSequence;

    public QueuedCommand Set(GameCommand command)
    {
        if (command.Action == GangAction.None)
            throw new ArgumentException("None cannot be queued for execution.", nameof(command));

        var queued = new QueuedCommand(_nextSequence++, command);
        _byGang[command.Gang] = queued;
        _executionPlan = null;
        return queued;
    }

    public bool Cancel(GangId gang)
    {
        if (!_byGang.Remove(gang)) return false;
        _executionPlan = null;
        return true;
    }

    public bool TryGet(GangId gang, out QueuedCommand? command) => _byGang.TryGetValue(gang, out command);

    /// <summary>
    /// The canonical execution order, rebuilt only after the queue changes. The plan is handed out
    /// by reference rather than copied, so it is wrapped the way <see cref="MatchState.Events"/>
    /// wraps its live list: an unwrapped array would let a caller cast the result back and reorder
    /// the queue in place, which state fingerprints would report as divergence rather than damage.
    /// </summary>
    public IReadOnlyList<QueuedCommand> ExecutionPlan() => _executionPlan ??= Array.AsReadOnly(
        _byGang.Values
            .OrderBy(command => TurnStructure.ExecutionIndex(command.ExecutionPhase))
            .ThenBy(command => command.Sequence)
            .ThenBy(command => command.Command.Player.Value)
            .ThenBy(command => command.Command.Gang.Value)
            .ToArray());

    public IReadOnlyList<QueuedCommand> ForPhase(ExecutionPhase phase) => ExecutionPlan()
        .Where(command => command.ExecutionPhase == phase)
        .ToArray();

    /// <summary>Ends resolution, preserving only commands explicitly marked to repeat.</summary>
    public void FinishExecution()
    {
        var changed = false;
        foreach (var gang in _byGang.Where(pair => !pair.Value.Command.Repeat).Select(pair => pair.Key).ToArray())
        {
            _byGang.Remove(gang);
            changed = true;
        }
        if (changed) _executionPlan = null;
    }

    public void Clear()
    {
        if (_byGang.Count == 0) return;
        _byGang.Clear();
        _executionPlan = null;
    }

    internal static TurnCommandQueue Restore(
        IReadOnlyList<QueuedCommand> commands,
        long nextSequence)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (nextSequence < 0) throw new ArgumentOutOfRangeException(nameof(nextSequence));
        if (commands.Select(command => command.Command.Gang).Distinct().Count() != commands.Count)
            throw new ArgumentException("A restored queue cannot contain multiple commands for one gang.", nameof(commands));
        if (commands.Select(command => command.Sequence).Distinct().Count() != commands.Count
            || commands.Any(command => command.Sequence < 0 || command.Sequence >= nextSequence))
            throw new ArgumentException("Restored command sequences are invalid.", nameof(commands));

        var queue = new TurnCommandQueue { _nextSequence = nextSequence };
        foreach (var command in commands) queue._byGang.Add(command.Command.Gang, command);
        return queue;
    }
}

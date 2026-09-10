namespace Rechaos.Core.GameModel;

/// <summary>
/// Original-compatible per-player hire roles and per-gang strategy families.
/// The original executable reserves 81 planning records for each of its six
/// player slots, independently of the recreation's active-gang limit.
/// </summary>
public sealed class AiPlanningState
{
    public const int GangSlotsPerPlayer = 81;
    public const int UnusedFamily = 99;
    public const int SectorAnchorOffset = MatchLimits.SectorCount;
    public const int InactiveSectorAnchor = 100 + SectorAnchorOffset;
    private const int MaximumHireRole = 6;
    private const int MaximumFamily = 14;

    private readonly int[] _currentHireRoles;
    private readonly int[] _previousHireRoles;
    private readonly int[] _families;
    private readonly int[] _sectorAnchors;
    private readonly GangAction[] _olderActions;
    private readonly GangAction[] _previousActions;
    private readonly GangAction[] _plannedActions;

    private AiPlanningState(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions)
    {
        ArgumentNullException.ThrowIfNull(currentHireRoles);
        ArgumentNullException.ThrowIfNull(previousHireRoles);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(sectorAnchors);
        ArgumentNullException.ThrowIfNull(olderActions);
        ArgumentNullException.ThrowIfNull(previousActions);
        ArgumentNullException.ThrowIfNull(plannedActions);
        if (currentHireRoles.Count != MatchLimits.PlayerCount
            || previousHireRoles.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI hire roles must contain all six original player slots.");
        if (families.Count != MatchLimits.PlayerCount * GangSlotsPerPlayer)
            throw new ArgumentException("AI families must contain all six-by-81 original planning slots.", nameof(families));
        if (sectorAnchors.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI sector anchors must contain all six original player slots.", nameof(sectorAnchors));
        var actionSlotCount = MatchLimits.PlayerCount * GangSlotsPerPlayer;
        if (olderActions.Count != actionSlotCount
            || previousActions.Count != actionSlotCount
            || plannedActions.Count != actionSlotCount)
            throw new ArgumentException("AI action histories must contain all six-by-81 original planning slots.");
        if (currentHireRoles.Any(role => role is < 0 or > MaximumHireRole))
            throw new ArgumentOutOfRangeException(nameof(currentHireRoles));
        if (previousHireRoles.Any(role => role is < 0 or > MaximumHireRole))
            throw new ArgumentOutOfRangeException(nameof(previousHireRoles));
        if (families.Any(family => !IsValidFamily(family)))
            throw new ArgumentOutOfRangeException(nameof(families));
        if (sectorAnchors.Any(anchor => !IsValidSectorAnchor(anchor)))
            throw new ArgumentOutOfRangeException(nameof(sectorAnchors));
        if (olderActions.Any(action => !IsValidAction(action))
            || previousActions.Any(action => !IsValidAction(action))
            || plannedActions.Any(action => !IsValidAction(action)))
            throw new ArgumentOutOfRangeException(nameof(olderActions));

        _currentHireRoles = currentHireRoles.ToArray();
        _previousHireRoles = previousHireRoles.ToArray();
        _families = families.ToArray();
        _sectorAnchors = sectorAnchors.ToArray();
        _olderActions = olderActions.ToArray();
        _previousActions = previousActions.ToArray();
        _plannedActions = plannedActions.ToArray();
    }

    public int CurrentHireRole(PlayerId player) => _currentHireRoles[PlayerIndex(player)];
    public int PreviousHireRole(PlayerId player) => _previousHireRoles[PlayerIndex(player)];
    public int Family(PlayerId player, int gangSlot) => _families[FamilyIndex(player, gangSlot)];
    public int SectorAnchor(PlayerId player) => _sectorAnchors[PlayerIndex(player)];
    public GangAction OlderAction(PlayerId player, int gangSlot) => _olderActions[GangSlotIndex(player, gangSlot)];
    public GangAction PreviousAction(PlayerId player, int gangSlot) => _previousActions[GangSlotIndex(player, gangSlot)];
    public GangAction PlannedAction(PlayerId player, int gangSlot) => _plannedActions[GangSlotIndex(player, gangSlot)];

    internal IReadOnlyList<int> CaptureCurrentHireRoles() => _currentHireRoles.ToArray();
    internal IReadOnlyList<int> CapturePreviousHireRoles() => _previousHireRoles.ToArray();
    internal IReadOnlyList<int> CaptureFamilies() => _families.ToArray();
    internal IReadOnlyList<int> CaptureSectorAnchors() => _sectorAnchors.ToArray();
    internal IReadOnlyList<GangAction> CaptureOlderActions() => _olderActions.ToArray();
    internal IReadOnlyList<GangAction> CapturePreviousActions() => _previousActions.ToArray();
    internal IReadOnlyList<GangAction> CapturePlannedActions() => _plannedActions.ToArray();

    internal void BeginPlanning(PlayerId player)
    {
        var index = PlayerIndex(player);
        _previousHireRoles[index] = _currentHireRoles[index];
    }

    internal void SetCurrentHireRole(PlayerId player, int role)
    {
        if (role is < 0 or > MaximumHireRole) throw new ArgumentOutOfRangeException(nameof(role));
        _currentHireRoles[PlayerIndex(player)] = role;
    }

    internal void SetFamily(PlayerId player, int gangSlot, int family)
    {
        if (!IsValidFamily(family))
            throw new ArgumentOutOfRangeException(nameof(family));
        _families[FamilyIndex(player, gangSlot)] = family;
    }

    internal void SetSectorAnchor(PlayerId player, int anchor)
    {
        if (!IsValidSectorAnchor(anchor)) throw new ArgumentOutOfRangeException(nameof(anchor));
        _sectorAnchors[PlayerIndex(player)] = anchor;
    }

    internal void RollActiveGangActions(PlayerId player, IReadOnlyList<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        if (gangs.Count > GangSlotsPerPlayer)
            throw new ArgumentException("Player gang list exceeds the original AI slot allocation.", nameof(gangs));
        for (var gangSlot = 0; gangSlot < gangs.Count; gangSlot++)
        {
            if (!gangs[gangSlot].IsActive) continue;
            var index = GangSlotIndex(player, gangSlot);
            _olderActions[index] = _previousActions[index];
            _previousActions[index] = _plannedActions[index];
            _plannedActions[index] = GangAction.None;
        }
    }

    internal void SetPlannedAction(PlayerId player, int gangSlot, GangAction action)
    {
        if (!IsValidAction(action)) throw new ArgumentOutOfRangeException(nameof(action));
        _plannedActions[GangSlotIndex(player, gangSlot)] = action;
    }

    internal static AiPlanningState Initialize() => new(
        new int[MatchLimits.PlayerCount],
        new int[MatchLimits.PlayerCount],
        Enumerable.Repeat(UnusedFamily, MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
        Enumerable.Repeat(InactiveSectorAnchor, MatchLimits.PlayerCount).ToArray(),
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer]);

    internal static AiPlanningState Initialize(IReadOnlyList<MatchPlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var planning = Initialize();
        foreach (var player in players)
        {
            var anchor = player.Gangs.Count > 0
                ? checked(player.Gangs[0].SectorId + SectorAnchorOffset)
                : InactiveSectorAnchor;
            planning.SetSectorAnchor(player.Id, anchor);
        }
        return planning;
    }

    internal static AiPlanningState Restore(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors) => Restore(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer]);

    internal static AiPlanningState Restore(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions) => new(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            olderActions, previousActions, plannedActions);

    private static bool IsValidFamily(int family) =>
        family == UnusedFamily || family is >= 0 and <= MaximumFamily and not 8;

    private static bool IsValidSectorAnchor(int anchor) =>
        anchor == SectorAnchorOffset - 1
        || anchor == InactiveSectorAnchor
        || anchor is >= SectorAnchorOffset and < SectorAnchorOffset + MatchLimits.SectorCount;

    private static bool IsValidAction(GangAction action) => action is >= GangAction.None and <= GangAction.Terminate;

    private static int FamilyIndex(PlayerId player, int gangSlot)
        => GangSlotIndex(player, gangSlot);

    private static int GangSlotIndex(PlayerId player, int gangSlot)
    {
        if (gangSlot is < 0 or >= GangSlotsPerPlayer)
            throw new ArgumentOutOfRangeException(nameof(gangSlot));
        return checked(PlayerIndex(player) * GangSlotsPerPlayer + gangSlot);
    }

    private static int PlayerIndex(PlayerId player)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        return player.Value;
    }
}

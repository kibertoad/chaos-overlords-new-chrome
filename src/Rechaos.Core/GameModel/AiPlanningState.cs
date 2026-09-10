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

    private AiPlanningState(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors)
    {
        ArgumentNullException.ThrowIfNull(currentHireRoles);
        ArgumentNullException.ThrowIfNull(previousHireRoles);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(sectorAnchors);
        if (currentHireRoles.Count != MatchLimits.PlayerCount
            || previousHireRoles.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI hire roles must contain all six original player slots.");
        if (families.Count != MatchLimits.PlayerCount * GangSlotsPerPlayer)
            throw new ArgumentException("AI families must contain all six-by-81 original planning slots.", nameof(families));
        if (sectorAnchors.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI sector anchors must contain all six original player slots.", nameof(sectorAnchors));
        if (currentHireRoles.Any(role => role is < 0 or > MaximumHireRole))
            throw new ArgumentOutOfRangeException(nameof(currentHireRoles));
        if (previousHireRoles.Any(role => role is < 0 or > MaximumHireRole))
            throw new ArgumentOutOfRangeException(nameof(previousHireRoles));
        if (families.Any(family => !IsValidFamily(family)))
            throw new ArgumentOutOfRangeException(nameof(families));
        if (sectorAnchors.Any(anchor => !IsValidSectorAnchor(anchor)))
            throw new ArgumentOutOfRangeException(nameof(sectorAnchors));

        _currentHireRoles = currentHireRoles.ToArray();
        _previousHireRoles = previousHireRoles.ToArray();
        _families = families.ToArray();
        _sectorAnchors = sectorAnchors.ToArray();
    }

    public int CurrentHireRole(PlayerId player) => _currentHireRoles[PlayerIndex(player)];
    public int PreviousHireRole(PlayerId player) => _previousHireRoles[PlayerIndex(player)];
    public int Family(PlayerId player, int gangSlot) => _families[FamilyIndex(player, gangSlot)];
    public int SectorAnchor(PlayerId player) => _sectorAnchors[PlayerIndex(player)];

    internal IReadOnlyList<int> CaptureCurrentHireRoles() => _currentHireRoles.ToArray();
    internal IReadOnlyList<int> CapturePreviousHireRoles() => _previousHireRoles.ToArray();
    internal IReadOnlyList<int> CaptureFamilies() => _families.ToArray();
    internal IReadOnlyList<int> CaptureSectorAnchors() => _sectorAnchors.ToArray();

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

    internal static AiPlanningState Initialize() => new(
        new int[MatchLimits.PlayerCount],
        new int[MatchLimits.PlayerCount],
        Enumerable.Repeat(UnusedFamily, MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
        Enumerable.Repeat(InactiveSectorAnchor, MatchLimits.PlayerCount).ToArray());

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
        IReadOnlyList<int> sectorAnchors) => new(
            currentHireRoles, previousHireRoles, families, sectorAnchors);

    private static bool IsValidFamily(int family) =>
        family == UnusedFamily || family is >= 0 and <= MaximumFamily and not 8;

    private static bool IsValidSectorAnchor(int anchor) =>
        anchor == SectorAnchorOffset - 1
        || anchor == InactiveSectorAnchor
        || anchor is >= SectorAnchorOffset and < SectorAnchorOffset + MatchLimits.SectorCount;

    private static int FamilyIndex(PlayerId player, int gangSlot)
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

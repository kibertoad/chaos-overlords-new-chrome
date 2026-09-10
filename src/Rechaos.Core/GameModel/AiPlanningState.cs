namespace Rechaos.Core.GameModel;

/// <summary>
/// The two command-dependent bytes stored beside each original AI action.
/// Their meaning is defined by <see cref="OriginalAiActionTargetEncoding"/>.
/// </summary>
public readonly record struct AiActionTarget(byte First, byte Second)
{
    public static AiActionTarget None => default;
}

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
    public const int InactiveFormationSector = -1;
    private const int MaximumHireRole = 6;
    private const int MaximumFamily = 14;

    private readonly int[] _currentHireRoles;
    private readonly int[] _previousHireRoles;
    private readonly int[] _families;
    private readonly int[] _sectorAnchors;
    private readonly GangAction[] _olderActions;
    private readonly GangAction[] _previousActions;
    private readonly GangAction[] _plannedActions;
    private readonly AiActionTarget[] _olderTargets;
    private readonly AiActionTarget[] _previousTargets;
    private readonly AiActionTarget[] _plannedTargets;
    private readonly bool[] _hasPlanned;
    private readonly short[] _weaponCooldowns;
    private readonly short[] _armorCooldowns;
    private readonly short[] _formationSectors;

    private AiPlanningState(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions,
        IReadOnlyList<AiActionTarget> olderTargets,
        IReadOnlyList<AiActionTarget> previousTargets,
        IReadOnlyList<AiActionTarget> plannedTargets,
        IReadOnlyList<bool> hasPlanned,
        IReadOnlyList<short> weaponCooldowns,
        IReadOnlyList<short> armorCooldowns,
        IReadOnlyList<short> formationSectors)
    {
        ArgumentNullException.ThrowIfNull(currentHireRoles);
        ArgumentNullException.ThrowIfNull(previousHireRoles);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(sectorAnchors);
        ArgumentNullException.ThrowIfNull(olderActions);
        ArgumentNullException.ThrowIfNull(previousActions);
        ArgumentNullException.ThrowIfNull(plannedActions);
        ArgumentNullException.ThrowIfNull(olderTargets);
        ArgumentNullException.ThrowIfNull(previousTargets);
        ArgumentNullException.ThrowIfNull(plannedTargets);
        ArgumentNullException.ThrowIfNull(hasPlanned);
        ArgumentNullException.ThrowIfNull(weaponCooldowns);
        ArgumentNullException.ThrowIfNull(armorCooldowns);
        ArgumentNullException.ThrowIfNull(formationSectors);
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
        if (olderTargets.Count != actionSlotCount
            || previousTargets.Count != actionSlotCount
            || plannedTargets.Count != actionSlotCount)
            throw new ArgumentException("AI action targets must contain all six-by-81 original planning slots.");
        if (hasPlanned.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI first-planning flags must contain all six original player slots.", nameof(hasPlanned));
        if (weaponCooldowns.Count != actionSlotCount || armorCooldowns.Count != actionSlotCount)
            throw new ArgumentException("AI equipment cooldowns must contain all six-by-81 original planning slots.");
        if (formationSectors.Count != actionSlotCount)
            throw new ArgumentException("AI formation sectors must contain all six-by-81 original planning slots.");
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
        if (formationSectors.Any(sector => sector != InactiveFormationSector
                && sector is < 0 or >= MatchLimits.SectorCount))
            throw new ArgumentOutOfRangeException(nameof(formationSectors));

        _currentHireRoles = currentHireRoles.ToArray();
        _previousHireRoles = previousHireRoles.ToArray();
        _families = families.ToArray();
        _sectorAnchors = sectorAnchors.ToArray();
        _olderActions = olderActions.ToArray();
        _previousActions = previousActions.ToArray();
        _plannedActions = plannedActions.ToArray();
        _olderTargets = olderTargets.ToArray();
        _previousTargets = previousTargets.ToArray();
        _plannedTargets = plannedTargets.ToArray();
        _hasPlanned = hasPlanned.ToArray();
        _weaponCooldowns = weaponCooldowns.ToArray();
        _armorCooldowns = armorCooldowns.ToArray();
        _formationSectors = formationSectors.ToArray();
    }

    public int CurrentHireRole(PlayerId player) => _currentHireRoles[PlayerIndex(player)];
    public int PreviousHireRole(PlayerId player) => _previousHireRoles[PlayerIndex(player)];
    public int Family(PlayerId player, int gangSlot) => _families[FamilyIndex(player, gangSlot)];
    public int SectorAnchor(PlayerId player) => _sectorAnchors[PlayerIndex(player)];
    public GangAction OlderAction(PlayerId player, int gangSlot) => _olderActions[GangSlotIndex(player, gangSlot)];
    public GangAction PreviousAction(PlayerId player, int gangSlot) => _previousActions[GangSlotIndex(player, gangSlot)];
    public GangAction PlannedAction(PlayerId player, int gangSlot) => _plannedActions[GangSlotIndex(player, gangSlot)];
    public AiActionTarget OlderTarget(PlayerId player, int gangSlot) => _olderTargets[GangSlotIndex(player, gangSlot)];
    public AiActionTarget PreviousTarget(PlayerId player, int gangSlot) => _previousTargets[GangSlotIndex(player, gangSlot)];
    public AiActionTarget PlannedTarget(PlayerId player, int gangSlot) => _plannedTargets[GangSlotIndex(player, gangSlot)];
    public bool HasPlanned(PlayerId player) => _hasPlanned[PlayerIndex(player)];
    public int WeaponCooldown(PlayerId player, int gangSlot) => _weaponCooldowns[GangSlotIndex(player, gangSlot)];
    public int ArmorCooldown(PlayerId player, int gangSlot) => _armorCooldowns[GangSlotIndex(player, gangSlot)];
    public int FormationSector(PlayerId player, int gangSlot) => _formationSectors[GangSlotIndex(player, gangSlot)];

    internal IReadOnlyList<int> CaptureCurrentHireRoles() => _currentHireRoles.ToArray();
    internal IReadOnlyList<int> CapturePreviousHireRoles() => _previousHireRoles.ToArray();
    internal IReadOnlyList<int> CaptureFamilies() => _families.ToArray();
    internal IReadOnlyList<int> CaptureSectorAnchors() => _sectorAnchors.ToArray();
    internal IReadOnlyList<GangAction> CaptureOlderActions() => _olderActions.ToArray();
    internal IReadOnlyList<GangAction> CapturePreviousActions() => _previousActions.ToArray();
    internal IReadOnlyList<GangAction> CapturePlannedActions() => _plannedActions.ToArray();
    internal IReadOnlyList<AiActionTarget> CaptureOlderTargets() => _olderTargets.ToArray();
    internal IReadOnlyList<AiActionTarget> CapturePreviousTargets() => _previousTargets.ToArray();
    internal IReadOnlyList<AiActionTarget> CapturePlannedTargets() => _plannedTargets.ToArray();
    internal IReadOnlyList<bool> CaptureHasPlanned() => _hasPlanned.ToArray();
    internal IReadOnlyList<short> CaptureWeaponCooldowns() => _weaponCooldowns.ToArray();
    internal IReadOnlyList<short> CaptureArmorCooldowns() => _armorCooldowns.ToArray();
    internal IReadOnlyList<short> CaptureFormationSectors() => _formationSectors.ToArray();

    internal bool BeginPlanning(PlayerId player)
    {
        var index = PlayerIndex(player);
        _previousHireRoles[index] = _currentHireRoles[index];
        if (!_hasPlanned[index])
        {
            for (var gangSlot = 0; gangSlot < GangSlotsPerPlayer; gangSlot++)
                ResetGangSlot(player, gangSlot);
            _hasPlanned[index] = true;
            return true;
        }
        return false;
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
            _olderTargets[index] = _previousTargets[index];
            _previousTargets[index] = _plannedTargets[index];
            _plannedTargets[index] = AiActionTarget.None;
        }
    }

    internal void RefreshEquipmentCooldowns(PlayerId player, IReadOnlyList<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        if (gangs.Count > GangSlotsPerPlayer)
            throw new ArgumentException("Player gang list exceeds the original AI slot allocation.", nameof(gangs));
        for (var gangSlot = 0; gangSlot < GangSlotsPerPlayer; gangSlot++)
        {
            var index = GangSlotIndex(player, gangSlot);
            if (gangSlot >= gangs.Count || !gangs[gangSlot].IsActive)
            {
                _weaponCooldowns[index] = 0;
                _armorCooldowns[index] = 0;
                continue;
            }

            _weaponCooldowns[index] = gangs[gangSlot].WeaponItemId is null
                ? (short)0
                : unchecked((short)(_weaponCooldowns[index] - 1));
            _armorCooldowns[index] = gangs[gangSlot].ArmorItemId is null
                ? (short)0
                : unchecked((short)(_armorCooldowns[index] - 1));
        }
    }

    internal void SetEquipmentCooldown(
        PlayerId player,
        int gangSlot,
        EquipmentSlot slot,
        int cooldown)
    {
        if (cooldown is < short.MinValue or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        var index = GangSlotIndex(player, gangSlot);
        switch (slot)
        {
            case EquipmentSlot.Weapon:
                _weaponCooldowns[index] = (short)cooldown;
                break;
            case EquipmentSlot.Armor:
                _armorCooldowns[index] = (short)cooldown;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    internal void SetFormationSector(PlayerId player, int gangSlot, int sectorId)
    {
        if (sectorId != InactiveFormationSector
            && sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        _formationSectors[GangSlotIndex(player, gangSlot)] = checked((short)sectorId);
    }

    internal void CleanupDuplicatePreviousActions(
        PlayerId player,
        IReadOnlyList<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        if (gangs.Count > GangSlotsPerPlayer)
            throw new ArgumentException("Player gang list exceeds the original AI slot allocation.", nameof(gangs));
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            RewriteFirstDuplicate(player, gangs, sectorId, GangAction.Chaos, GangAction.None);
            RewriteFirstDuplicate(player, gangs, sectorId, GangAction.Influence, GangAction.Snitch);
        }
    }

    internal void SetPlannedAction(PlayerId player, int gangSlot, GangAction action)
        => SetPlannedAction(player, gangSlot, action, AiActionTarget.None);

    internal void SetPlannedAction(
        PlayerId player,
        int gangSlot,
        GangAction action,
        AiActionTarget target)
    {
        if (!IsValidAction(action)) throw new ArgumentOutOfRangeException(nameof(action));
        var index = GangSlotIndex(player, gangSlot);
        _plannedActions[index] = action;
        _plannedTargets[index] = target;
    }

    internal void ResetGangSlot(PlayerId player, int gangSlot)
    {
        var index = GangSlotIndex(player, gangSlot);
        _families[index] = UnusedFamily;
        _olderActions[index] = GangAction.None;
        _previousActions[index] = GangAction.None;
        _plannedActions[index] = GangAction.None;
        _olderTargets[index] = AiActionTarget.None;
        _previousTargets[index] = AiActionTarget.None;
        _plannedTargets[index] = AiActionTarget.None;
        _weaponCooldowns[index] = 0;
        _armorCooldowns[index] = 0;
        _formationSectors[index] = InactiveFormationSector;
    }

    private void RewriteFirstDuplicate(
        PlayerId player,
        IReadOnlyList<MatchGangState> gangs,
        int sectorId,
        GangAction action,
        GangAction replacement)
    {
        var matchingSlots = Enumerable.Range(0, gangs.Count)
            .Where(slot => gangs[slot].IsActive
                && gangs[slot].SectorId == sectorId
                && PreviousAction(player, slot) == action)
            .ToArray();
        if (matchingSlots.Length > 1)
            _previousActions[GangSlotIndex(player, matchingSlots[0])] = replacement;
    }

    internal static AiPlanningState Initialize() => new(
        new int[MatchLimits.PlayerCount],
        new int[MatchLimits.PlayerCount],
        Enumerable.Repeat(UnusedFamily, MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
        Enumerable.Repeat(InactiveSectorAnchor, MatchLimits.PlayerCount).ToArray(),
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new bool[MatchLimits.PlayerCount],
        new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        Enumerable.Repeat(
            checked((short)InactiveFormationSector),
            MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray());

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
            for (var gangSlot = 0; gangSlot < player.Gangs.Count; gangSlot++)
                if (player.Gangs[gangSlot].IsActive)
                    planning.SetFormationSector(
                        player.Id, gangSlot, player.Gangs[gangSlot].SectorId);
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
            new GangAction[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            Enumerable.Repeat(true, MatchLimits.PlayerCount).ToArray());

    internal static AiPlanningState Restore(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions) => Restore(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            olderActions, previousActions, plannedActions,
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            Enumerable.Repeat(true, MatchLimits.PlayerCount).ToArray());

    internal static AiPlanningState Restore(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions,
        IReadOnlyList<bool> hasPlanned) => Restore(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            olderActions, previousActions, plannedActions,
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            new AiActionTarget[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            hasPlanned);

    internal static AiPlanningState Restore(
        IReadOnlyList<int> currentHireRoles,
        IReadOnlyList<int> previousHireRoles,
        IReadOnlyList<int> families,
        IReadOnlyList<int> sectorAnchors,
        IReadOnlyList<GangAction> olderActions,
        IReadOnlyList<GangAction> previousActions,
        IReadOnlyList<GangAction> plannedActions,
        IReadOnlyList<AiActionTarget> olderTargets,
        IReadOnlyList<AiActionTarget> previousTargets,
        IReadOnlyList<AiActionTarget> plannedTargets,
        IReadOnlyList<bool> hasPlanned,
        IReadOnlyList<short>? weaponCooldowns = null,
        IReadOnlyList<short>? armorCooldowns = null,
        IReadOnlyList<short>? formationSectors = null) => new(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            olderActions, previousActions, plannedActions,
            olderTargets, previousTargets, plannedTargets, hasPlanned,
            weaponCooldowns ?? new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            armorCooldowns ?? new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            formationSectors ?? Enumerable.Repeat(
                checked((short)InactiveFormationSector),
                MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray());

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

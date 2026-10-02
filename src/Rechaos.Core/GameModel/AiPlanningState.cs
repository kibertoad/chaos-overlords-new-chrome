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
/// Original-compatible per-player hire roles, per-gang strategy families, and
/// polymorphic auxiliary values used by recovered family handlers.
/// The original executable reserves 81 planning records for each of its six
/// player slots, independently of the recreation's active-gang limit.
/// </summary>
public sealed class AiPlanningState
{
    public const int GangSlotsPerPlayer = 81;
    public const int UnusedFamily = 99;
    public const int RaiderFamily = 9;
    public const int SectorAnchorOffset = MatchLimits.SectorCount;
    public const int InactiveSectorAnchor = 100 + SectorAnchorOffset;
    public const int InactiveFormationSector = -1;
    public const int InactiveFocusValue = InactiveFormationSector;
    public const int InactiveCoverageSector = -1;
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
    private readonly short[] _focusValues;
    private readonly short[] _coverageSectors;
    private readonly bool[] _needsFamily;
    private readonly bool[] _raiderMode;
    private readonly byte[] _sectorWeights;
    private readonly int[] _sectorChoiceScores;
    private byte _firstCombatRecordDefinition;

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
        IReadOnlyList<short> focusValues,
        IReadOnlyList<short> coverageSectors,
        IReadOnlyList<bool> needsFamily,
        IReadOnlyList<bool> raiderMode,
        byte firstCombatRecordDefinition,
        IReadOnlyList<byte> sectorWeights,
        IReadOnlyList<int> sectorChoiceScores)
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
        ArgumentNullException.ThrowIfNull(focusValues);
        ArgumentNullException.ThrowIfNull(coverageSectors);
        ArgumentNullException.ThrowIfNull(needsFamily);
        ArgumentNullException.ThrowIfNull(raiderMode);
        ArgumentNullException.ThrowIfNull(sectorWeights);
        ArgumentNullException.ThrowIfNull(sectorChoiceScores);
        if (sectorChoiceScores.Count != MatchLimits.SectorCount)
            throw new ArgumentException("The sector choice scores must hold one value per sector.", nameof(sectorChoiceScores));
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
        if (focusValues.Count != actionSlotCount)
            throw new ArgumentException("AI focus values must contain all six-by-81 original planning slots.");
        if (coverageSectors.Count != actionSlotCount)
            throw new ArgumentException("AI coverage sectors must contain all six-by-81 original planning slots.");
        if (needsFamily.Count != actionSlotCount)
            throw new ArgumentException("AI family flags must contain all six-by-81 original planning slots.");
        if (raiderMode.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI raider flags must contain all six original player slots.", nameof(raiderMode));
        if (sectorWeights.Count != MatchLimits.PlayerCount * MatchLimits.SectorCount)
            throw new ArgumentException("AI sector weights must contain all six-by-64 original cells.", nameof(sectorWeights));
        if (sectorWeights.Any(weight => weight is not (0 or 1 or 10)))
            throw new ArgumentOutOfRangeException(nameof(sectorWeights));
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
        if (focusValues.Any(value => value != InactiveFocusValue
                && value is < 0 or >= MatchLimits.SectorCount))
            throw new ArgumentOutOfRangeException(nameof(focusValues));
        if (coverageSectors.Any(value => value != InactiveCoverageSector
                && value is < 0 or >= MatchLimits.SectorCount))
            throw new ArgumentOutOfRangeException(nameof(coverageSectors));

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
        _focusValues = focusValues.ToArray();
        _coverageSectors = coverageSectors.ToArray();
        _needsFamily = needsFamily.ToArray();
        _raiderMode = raiderMode.ToArray();
        _sectorWeights = sectorWeights.ToArray();
        _sectorChoiceScores = sectorChoiceScores.ToArray();
        _firstCombatRecordDefinition = RequireUnsignedAgnostic(firstCombatRecordDefinition);
    }

    public int CurrentHireRole(PlayerId player) => _currentHireRoles[PlayerIndex(player)];
    public int PreviousHireRole(PlayerId player) => _previousHireRoles[PlayerIndex(player)];
    public int Family(PlayerId player, int gangSlot) => _families[FamilyIndex(player, gangSlot)];

    /// <summary>A live, uncopied view of the player's 81 family bytes in roster slot order.</summary>
    public IReadOnlyList<int> Families(PlayerId player) =>
        new ArraySegment<int>(_families, FamilyIndex(player, 0), GangSlotsPerPlayer);
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
    public int FormationSector(PlayerId player, int gangSlot) => _focusValues[GangSlotIndex(player, gangSlot)];
    public int FocusValue(PlayerId player, int gangSlot) => FormationSector(player, gangSlot);
    public int CoverageSector(PlayerId player, int gangSlot) =>
        _coverageSectors[GangSlotIndex(player, gangSlot)];

    /// <summary>RULE-AI-002: whether the slot's next dispatch resets its record and assigns a family.</summary>
    public bool NeedsFamily(PlayerId player, int gangSlot) =>
        _needsFamily[GangSlotIndex(player, gangSlot)];

    /// <summary>RULE-AI-001, RULE-AI-027: whether every active gang of the player plans as family 9.</summary>
    public bool RaiderMode(PlayerId player) => _raiderMode[PlayerIndex(player)];

    /// <summary>
    /// RULE-AI-003: the weight the player's last planning pass cached for the sector, from
    /// RULE-AI-004's visible_weight before that pass's hostility step: 10, 1 or 0.
    /// </summary>
    public int SectorWeight(PlayerId player, int sectorId) =>
        _sectorWeights[SectorWeightIndex(player, sectorId)];

    /// <summary>The player's 64 cached sector weights (RULE-AI-003), for its pass to overwrite.</summary>
    internal Span<byte> SectorWeightRow(PlayerId player) =>
        _sectorWeights.AsSpan(PlayerIndex(player) * MatchLimits.SectorCount, MatchLimits.SectorCount);

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
    internal IReadOnlyList<short> CaptureFormationSectors() => _focusValues.ToArray();
    internal IReadOnlyList<short> CaptureCoverageSectors() => _coverageSectors.ToArray();
    internal IReadOnlyList<bool> CaptureNeedsFamily() => _needsFamily.ToArray();
    internal IReadOnlyList<bool> CaptureRaiderMode() => _raiderMode.ToArray();
    internal IReadOnlyList<byte> CaptureSectorWeights() => _sectorWeights.ToArray();
    internal IReadOnlyList<int> CaptureSectorChoiceScores() => _sectorChoiceScores.ToArray();

    /// <summary>
    /// RULE-AI-006, FND-AI-066: the score half of the sector selector's 64 pairs. The original keeps
    /// them in one global list that every call of the selector, for any player, sorts and leaves
    /// behind, and a call copies a new score into a pair only for the sectors its late filter keeps.
    /// </summary>
    internal int[] SectorChoiceScores => _sectorChoiceScores;

    /// <summary>
    /// FMT-STATE-007: the six blocks of 81 planning records as the original holds them in memory,
    /// the bytes the sector selector's unbounded tie count reads past its own lists (FND-AI-066).
    /// A player that has never had a planning pass keeps records of zero bytes; the first pass
    /// resets them (RULE-AI-001).
    /// </summary>
    internal byte[] PlanningRecordImage()
    {
        var image = new byte[MatchLimits.PlayerCount * GangSlotsPerPlayer * PlanningRecordSize];
        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            if (!_hasPlanned[player]) continue;
            for (var slot = 0; slot < GangSlotsPerPlayer; slot++)
            {
                var index = player * GangSlotsPerPlayer + slot;
                var record = image.AsSpan(index * PlanningRecordSize, PlanningRecordSize);
                record[0] = unchecked((byte)_families[index]);
                record[1] = _needsFamily[index] ? (byte)1 : (byte)0;
                record[2] = (byte)_olderActions[index];
                record[3] = _olderTargets[index].First;
                record[4] = _olderTargets[index].Second;
                record[5] = (byte)_previousActions[index];
                record[6] = _previousTargets[index].First;
                record[7] = _previousTargets[index].Second;
                record[8] = (byte)_plannedActions[index];
                record[9] = _plannedTargets[index].First;
                record[10] = _plannedTargets[index].Second;
                BitConverter.TryWriteBytes(record[12..], _weaponCooldowns[index]);
                BitConverter.TryWriteBytes(record[14..], _armorCooldowns[index]);
            }
        }
        return image;
    }

    internal const int PlanningRecordSize = 16;

    /// <summary>
    /// Byte 0, <c>definition</c>, of the first combat record (FMT-STATE-003): player 0's roster
    /// slot 0. The computer players read it as the owner of sector index 64, one past the last
    /// sector (RULE-AI-005, RULE-AI-013). It is 0 until that slot's gang fights; RULE-COMBAT-002
    /// then writes the definition of the gang holding the slot, and the value stays through later
    /// phases in which the slot does not fight, after the gang dies and after a hire reuses the
    /// slot (FND-STATE-005).
    /// FMT-STATE-003 does not record whether the original loads the byte signed. The value is held
    /// within 0..127, where both readings agree and it is never the neutral owner -1: the gang
    /// definitions are the 90 records of FMT-DATA-002, and a larger value is refused.
    /// </summary>
    public int FirstCombatRecordDefinition => _firstCombatRecordDefinition;

    internal void RecordFirstCombatRecordDefinition(short definitionId) =>
        // The record stores the low byte of the gang's definition byte (FND-STATE-005).
        _firstCombatRecordDefinition = RequireUnsignedAgnostic(unchecked((byte)definitionId));

    private static byte RequireUnsignedAgnostic(byte definition) =>
        definition <= sbyte.MaxValue
            ? definition
            : throw new ArgumentOutOfRangeException(
                nameof(definition), definition,
                "The first combat record's definition must read the same signed and unsigned.");

    /// <summary>
    /// RULE-AI-001: on the player's first pass every record is reset, both hire roles become 0
    /// and only slot 0 is flagged for a family. Then the previous hire role takes the current one.
    /// </summary>
    internal bool BeginPlanning(PlayerId player)
    {
        var index = PlayerIndex(player);
        var firstPass = !_hasPlanned[index];
        if (firstPass)
        {
            for (var gangSlot = 0; gangSlot < GangSlotsPerPlayer; gangSlot++)
                ResetGangSlot(player, gangSlot);
            _currentHireRoles[index] = 0;
            _needsFamily[GangSlotIndex(player, 0)] = true;
            _hasPlanned[index] = true;
        }
        _previousHireRoles[index] = _currentHireRoles[index];
        return firstPass;
    }

    /// <summary>
    /// RULE-AI-001: a slot with no active gang is flagged for a family; its history stays until
    /// the new gang's first dispatch wipes it.
    /// </summary>
    internal void FlagInactiveSlots(PlayerId player, IReadOnlyList<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        for (var gangSlot = 0; gangSlot < GangSlotsPerPlayer; gangSlot++)
            if (gangSlot >= gangs.Count || !gangs[gangSlot].IsActive)
                _needsFamily[GangSlotIndex(player, gangSlot)] = true;
    }

    /// <summary>
    /// RULE-AI-001: sets only the player's raider flag, with no reset of its records, for a test
    /// that follows a recording which wrote the original's byte directly.
    /// </summary>
    internal void SetRaiderMode(PlayerId player) => _raiderMode[PlayerIndex(player)] = true;

    internal void SetNeedsFamily(PlayerId player, int gangSlot) =>
        _needsFamily[GangSlotIndex(player, gangSlot)] = true;

    internal void ClearNeedsFamily(PlayerId player, int gangSlot) =>
        _needsFamily[GangSlotIndex(player, gangSlot)] = false;

    /// <summary>
    /// RULE-AI-027, FND-AI-043: a computer player taking over a network seat marks the player as
    /// started and a raider, resets all 81 records and writes family 9 to every active gang's.
    /// </summary>
    internal void EnterRaiderMode(PlayerId player, IReadOnlyList<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        var index = PlayerIndex(player);
        _hasPlanned[index] = true;
        _raiderMode[index] = true;
        for (var gangSlot = 0; gangSlot < GangSlotsPerPlayer; gangSlot++)
            ResetGangSlot(player, gangSlot);
        for (var gangSlot = 0; gangSlot < gangs.Count; gangSlot++)
            if (gangs[gangSlot].IsActive)
                _families[FamilyIndex(player, gangSlot)] = RaiderFamily;
    }

    /// <summary>
    /// RULE-AI-002: a flagged record is wiped (family 99, no actions or targets, no cooldowns) and
    /// its flag cleared before the dispatcher gives it a family. The auxiliary values stay.
    /// </summary>
    internal void ResetForNewFamily(PlayerId player, int gangSlot)
    {
        var index = GangSlotIndex(player, gangSlot);
        _families[index] = UnusedFamily;
        _needsFamily[index] = false;
        _olderActions[index] = GangAction.None;
        _previousActions[index] = GangAction.None;
        _plannedActions[index] = GangAction.None;
        _olderTargets[index] = AiActionTarget.None;
        _previousTargets[index] = AiActionTarget.None;
        _plannedTargets[index] = AiActionTarget.None;
        _weaponCooldowns[index] = 0;
        _armorCooldowns[index] = 0;
    }

    internal void SetCurrentHireRole(PlayerId player, int role)
    {
        if (role is < 0 or > MaximumHireRole) throw new ArgumentOutOfRangeException(nameof(role));
        _currentHireRoles[PlayerIndex(player)] = role;
    }

    /// <summary>
    /// Stores the family and nothing else. The needs_family flag is left alone, so the RULE-AI-010
    /// rewrite of a surplus hunter keeps a flag a Greed Terminate set earlier in the pass
    /// (RULE-AI-025, RULE-AI-030); only <see cref="ResetForNewFamily"/> clears it.
    /// </summary>
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
        _focusValues[GangSlotIndex(player, gangSlot)] = checked((short)sectorId);
    }

    internal void SetFocusValue(PlayerId player, int gangSlot, int value) =>
        SetFormationSector(player, gangSlot, value);

    internal void SetCoverageSector(PlayerId player, int gangSlot, int sectorId)
    {
        if (sectorId != InactiveCoverageSector
            && sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        _coverageSectors[GangSlotIndex(player, gangSlot)] = checked((short)sectorId);
    }

    internal void ClearPreviousTargetFirst(PlayerId player, int gangSlot)
    {
        var index = GangSlotIndex(player, gangSlot);
        _previousTargets[index] = new AiActionTarget(0, _previousTargets[index].Second);
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

    /// <summary>The first pass's reset of RULE-AI-001, which also clears both auxiliary values.</summary>
    internal void ResetGangSlot(PlayerId player, int gangSlot)
    {
        ResetForNewFamily(player, gangSlot);
        var index = GangSlotIndex(player, gangSlot);
        _focusValues[index] = InactiveFocusValue;
        _coverageSectors[index] = InactiveCoverageSector;
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
            MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
        Enumerable.Repeat(
            checked((short)InactiveCoverageSector),
            MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
        new bool[MatchLimits.PlayerCount * GangSlotsPerPlayer],
        new bool[MatchLimits.PlayerCount],
        0,
        new byte[MatchLimits.PlayerCount * MatchLimits.SectorCount],
        new int[MatchLimits.SectorCount]);

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
        IReadOnlyList<short>? formationSectors = null,
        IReadOnlyList<short>? coverageSectors = null,
        IReadOnlyList<bool>? needsFamily = null,
        IReadOnlyList<bool>? raiderMode = null,
        byte firstCombatRecordDefinition = 0,
        IReadOnlyList<byte>? sectorWeights = null,
        IReadOnlyList<int>? sectorChoiceScores = null) => new(
            currentHireRoles, previousHireRoles, families, sectorAnchors,
            olderActions, previousActions, plannedActions,
            olderTargets, previousTargets, plannedTargets, hasPlanned,
            weaponCooldowns ?? new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            armorCooldowns ?? new short[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            formationSectors ?? Enumerable.Repeat(
                checked((short)InactiveFormationSector),
                MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
            coverageSectors ?? Enumerable.Repeat(
                checked((short)InactiveCoverageSector),
                MatchLimits.PlayerCount * GangSlotsPerPlayer).ToArray(),
            needsFamily ?? new bool[MatchLimits.PlayerCount * GangSlotsPerPlayer],
            raiderMode ?? new bool[MatchLimits.PlayerCount],
            firstCombatRecordDefinition,
            sectorWeights ?? new byte[MatchLimits.PlayerCount * MatchLimits.SectorCount],
            sectorChoiceScores ?? new int[MatchLimits.SectorCount]);

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

    private static int SectorWeightIndex(PlayerId player, int sectorId)
    {
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return checked(PlayerIndex(player) * MatchLimits.SectorCount + sectorId);
    }

    private static int PlayerIndex(PlayerId player)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        return player.Value;
    }
}

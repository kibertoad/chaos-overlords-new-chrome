using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public enum PlayerController : byte
{
    Human,
    Computer
}

public enum AiDifficulty : byte
{
    Goon,
    Criminal,
    CrimeLord,
    HomicidalManiac
}

public enum PlayerStatus : byte
{
    Active,
    Eliminated
}

public sealed partial class MatchPlayerState
{
    private readonly List<MatchGangState> _gangs;
    private readonly HireOfferSlotState[] _hireOfferSlots;
    private readonly List<PendingHireState> _pendingHires;
    private readonly Dictionary<short, int> _researchProgress;
    private readonly HashSet<short> _researchedItems;
    private readonly Dictionary<short, int> _inventory;
    private MatchGangIndex? _gangIndex;

    public MatchPlayerState(
        MatchPlayerSetup setup,
        int cash,
        IReadOnlyList<MatchGangState>? gangs = null,
        IReadOnlyList<short>? hirePool = null,
        IReadOnlyList<PendingHireState>? pendingHires = null,
        IReadOnlyDictionary<short, int>? researchProgress = null,
        IReadOnlySet<short>? researchedItems = null,
        IReadOnlyDictionary<short, int>? inventory = null,
        int support = 0,
        int bigManPoints = 0,
        PlayerStatus status = PlayerStatus.Active,
        MatchStatistics? statistics = null,
        short? snubbedHireOffer = null,
        IReadOnlyList<HireOfferSlotState>? hireOfferSlots = null,
        int? snubbedHireOfferSlot = null,
        bool usesMaximumHireForce = false)
    {
        if (bigManPoints < 0) throw new ArgumentOutOfRangeException(nameof(bigManPoints));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _gangs = gangs?.ToList() ?? [];
        var compactHirePool = hirePool?.ToArray() ?? [];
        if (compactHirePool.Length > MatchLimits.HireOffersPerPlayer)
            throw new ArgumentException("Hire pool exceeds the three original offer slots.", nameof(hirePool));
        _hireOfferSlots = hireOfferSlots?.ToArray()
            ?? Enumerable.Range(0, MatchLimits.HireOffersPerPlayer)
                .Select(slot => slot < compactHirePool.Length
                    ? HireOfferSlotState.Available(compactHirePool[slot])
                    : HireOfferSlotState.Uninitialized)
                .ToArray();
        if (_hireOfferSlots.Length != MatchLimits.HireOffersPerPlayer)
            throw new ArgumentException("Hire offers must contain exactly three fixed slots.", nameof(hireOfferSlots));
        _pendingHires = pendingHires?.ToList() ?? [];
        _researchProgress = researchProgress?.ToDictionary() ?? [];
        _researchedItems = researchedItems is null ? [] : new HashSet<short>(researchedItems);
        _inventory = inventory?.ToDictionary() ?? [];
        Cash = cash;
        Support = support;
        BigManPoints = bigManPoints;
        Status = status;
        Statistics = statistics ?? new MatchStatistics();
        SnubbedHireOffer = snubbedHireOffer;
        SnubbedHireOfferSlot = snubbedHireOfferSlot;
        UsesMaximumHireForce = usesMaximumHireForce;
    }

    public MatchPlayerSetup Setup { get; internal set; }
    public PlayerId Id => Setup.Id;
    public PlayerStatus Status { get; internal set; } = PlayerStatus.Active;
    public int Cash { get; internal set; }
    public int Support { get; internal set; }
    public int BigManPoints { get; internal set; }
    public IReadOnlyList<MatchGangState> Gangs => _gangs;
    public IReadOnlyList<PendingHireState> PendingHires => _pendingHires;
    public IReadOnlyDictionary<short, int> ResearchProgress => _researchProgress;
    public IReadOnlySet<short> ResearchedItems => _researchedItems;
    public IReadOnlyDictionary<short, int> Inventory => _inventory;
    public MatchStatistics Statistics { get; }
    public short? SnubbedHireOffer { get; private set; }
    public int? SnubbedHireOfferSlot { get; private set; }
    public bool UsesMaximumHireForce { get; }
    public bool HasSnubbedHireOfferThisTurn => SnubbedHireOffer.HasValue;

    public int RemainingResearch(OriginalData definitions, short itemIndex)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ValidateResearchItem(definitions, itemIndex);
        if (_researchedItems.Contains(itemIndex)) return 0;
        return _researchProgress.GetValueOrDefault(itemIndex, definitions.Items[itemIndex].ResearchDifficulty);
    }

    internal int ApplyResearch(OriginalData definitions, short itemIndex, int successes)
    {
        var remaining = ManualRules.ApplyResearchProgress(RemainingResearch(definitions, itemIndex), successes);
        if (remaining == 0)
        {
            _researchProgress.Remove(itemIndex);
            _researchedItems.Add(itemIndex);
        }
        else
        {
            _researchProgress[itemIndex] = remaining;
        }
        return remaining;
    }

    // The match's gang index is told before the roster changes, so an identifier it refuses leaves
    // the roster as it was. A player composed before its match has no index yet; the match indexes
    // whatever its rosters already hold when it takes them over.
    internal void AddGang(MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(gang);
        _gangIndex?.Add(gang);
        _gangs.Add(gang);
    }

    internal void ReplaceGang(int gangSlot, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(gang);
        if (gangSlot is < 0 || gangSlot >= _gangs.Count)
            throw new ArgumentOutOfRangeException(nameof(gangSlot));
        _gangIndex?.Replace(_gangs[gangSlot], gang);
        _gangs[gangSlot] = gang;
    }

    /// <summary>
    /// Binds this player to the match owning <paramref name="index"/>. A player belongs to one
    /// match for the rest of its life: were an instance shared, the match that took it first would
    /// answer <see cref="MatchState.FindGang"/> from an index nobody maintains any more, so the
    /// second match refuses the instance instead.
    /// </summary>
    internal void JoinMatch(MatchGangIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (_gangIndex is not null)
            throw new InvalidOperationException("This player already belongs to a match.");
        _gangIndex = index;
    }

    internal void AddPendingHire(PendingHireState hire) => _pendingHires.Add(hire);
    internal void ClearPendingHires() => _pendingHires.Clear();

    private static void ValidateResearchItem(OriginalData definitions, short itemIndex)
    {
        if (!MatchState.IsActualItem(definitions, itemIndex))
            throw new ArgumentOutOfRangeException(nameof(itemIndex));
    }
}

public sealed class MatchGangState
{
    public MatchGangState(
        GangId id,
        PlayerId owner,
        short definitionId,
        int sectorId,
        int force,
        short? weaponItemId = null,
        short? armorItemId = null,
        short? miscellaneousItemId = null)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        if (force is < 0 or > ManualRules.MaximumForce)
            throw new ArgumentOutOfRangeException(nameof(force));
        Id = id;
        Owner = owner;
        DefinitionId = definitionId;
        SectorId = sectorId;
        Force = force;
        WeaponItemId = weaponItemId;
        ArmorItemId = armorItemId;
        MiscellaneousItemId = miscellaneousItemId;
    }

    public GangId Id { get; }
    public PlayerId Owner { get; }
    public short DefinitionId { get; }
    public int SectorId { get; internal set; }
    public int Force { get; internal set; }
    public bool Hidden { get; internal set; }
    public bool HiredThisTurn { get; internal set; }
    public short? WeaponItemId { get; internal set; }
    public short? ArmorItemId { get; internal set; }
    public short? MiscellaneousItemId { get; internal set; }
    public QueuedCommand? QueuedCommand { get; internal set; }
    public bool IsActive => Force > 0;
}

public sealed record PendingHireState(
    short GangDefinitionId,
    int TargetSectorId,
    int OfferSlot = -1,
    bool InitialCostPaid = false);

public sealed class MatchSectorState
{
    private readonly List<int> _crackdownHistory;

    public MatchSectorState(
        int id,
        IReadOnlyList<MatchSiteState> sites,
        PlayerId? owner = null,
        int tolerance = ManualRules.MinimumTolerance,
        int chaos = 0,
        bool crackdownActive = false,
        bool isImportant = false,
        int income = ManualRules.MinimumSectorIncome,
        int crackdownTurnsRemaining = 0,
        IReadOnlyList<int>? crackdownHistory = null,
        int? baseTolerance = null)
    {
        if (id is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(sites);
        if (sites.Count != MatchLimits.SitesPerSector)
            throw new ArgumentException($"A sector must contain exactly {MatchLimits.SitesPerSector} sites.", nameof(sites));
        if (sites.Select(site => site.Slot).Order().SequenceEqual(Enumerable.Range(0, MatchLimits.SitesPerSector)) is false)
            throw new ArgumentException("Site slots must be exactly 0, 1, and 2.", nameof(sites));
        if (chaos < 0) throw new ArgumentOutOfRangeException(nameof(chaos));
        if (income < 0) throw new ArgumentOutOfRangeException(nameof(income));
        if (crackdownTurnsRemaining < 0) throw new ArgumentOutOfRangeException(nameof(crackdownTurnsRemaining));
        if (!crackdownActive && crackdownTurnsRemaining != 0)
            throw new ArgumentException("Inactive police cannot have turns remaining.", nameof(crackdownTurnsRemaining));
        if (crackdownHistory is { Count: > 2 }
            || crackdownHistory?.Any(turn => turn < 1) == true
            || crackdownHistory?.Zip(crackdownHistory.Skip(1), (left, right) => left > right).Any(invalid => invalid) == true)
            throw new ArgumentException("Crackdown history must contain at most two nondecreasing positive turns.", nameof(crackdownHistory));
        Id = id;
        Sites = sites.OrderBy(site => site.Slot).ToArray();
        Owner = owner;
        Tolerance = tolerance;
        BaseTolerance = baseTolerance ?? tolerance;
        LegacyChaos = chaos;
        // Only a newly activated sector needs the minimum; restores preserve a decayed duration.
        CrackdownTurnsRemaining = crackdownActive
            ? crackdownTurnsRemaining > 0 ? crackdownTurnsRemaining : ManualRules.MinimumCrackdownTurns
            : 0;
        IsImportant = isImportant;
        Income = income;
        _crackdownHistory = crackdownHistory?.ToList() ?? [];
    }

    public int Id { get; }
    public IReadOnlyList<MatchSiteState> Sites { get; }
    public PlayerId? Owner { get; internal set; }

    /// <summary>
    /// The Tolerance the Chaos test compares with: the base plus the completed sites' Tolerance,
    /// rebuilt before planning and left alone during resolution (RULE-SITE-001).
    /// </summary>
    public int Tolerance { get; internal set; }

    /// <summary>
    /// The Tolerance without the sites' part, which Bribe, Snitch, the return toward normal and
    /// the clamp change during resolution (RULE-TOLERANCE-001, RULE-TOLERANCE-002). FMT-STATE-002
    /// keeps it as a signed byte.
    /// </summary>
    public int BaseTolerance { get; internal set; }
    // Retained only so pre-v24 saves/replays can verify their historical fingerprints.
    // The original executable has no persistent sector-Chaos accumulator.
    internal int LegacyChaos { get; }
    public bool CrackdownActive
    {
        get => CrackdownTurnsRemaining > 0;
        internal set => CrackdownTurnsRemaining = value
            ? Math.Max(ManualRules.MinimumCrackdownTurns, CrackdownTurnsRemaining)
            : 0;
    }
    public int CrackdownTurnsRemaining { get; internal set; }
    public IReadOnlyList<int> CrackdownHistory => _crackdownHistory;
    public bool IsImportant { get; }
    public int Income { get; }

    internal bool RecordCrackdown(int turn)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        if (_crackdownHistory.Count > 0 && turn <= _crackdownHistory[^1])
            throw new InvalidOperationException("A sector can record at most one Crackdown per turn.");
        _crackdownHistory.RemoveAll(previous => previous < turn - 5);
        var losesControl = _crackdownHistory.Count >= 2;
        if (losesControl)
        {
            _crackdownHistory.Clear();
            _crackdownHistory.Add(turn);
        }
        _crackdownHistory.Add(turn);
        return losesControl;
    }
}

public sealed class MatchSiteState
{
    public MatchSiteState(int slot, short definitionId, int resistance, PlayerId? influencedBy = null)
    {
        if (slot is < 0 or >= MatchLimits.SitesPerSector) throw new ArgumentOutOfRangeException(nameof(slot));
        if (resistance < 0) throw new ArgumentOutOfRangeException(nameof(resistance));
        Slot = slot;
        DefinitionId = definitionId;
        Resistance = resistance;
        InfluencedBy = influencedBy;
    }

    public int Slot { get; }
    public short DefinitionId { get; }
    public int Resistance { get; internal set; }
    public PlayerId? InfluencedBy { get; internal set; }
}

public sealed class MatchStatistics
{
    public MatchStatistics(
        long cashEarned = 0,
        long cashSpent = 0,
        int damageInflicted = 0,
        int casualties = 0,
        int overthrows = 0,
        int timesHidden = 0)
    {
        if (cashEarned < 0) throw new ArgumentOutOfRangeException(nameof(cashEarned));
        if (cashSpent < 0) throw new ArgumentOutOfRangeException(nameof(cashSpent));
        if (damageInflicted < 0) throw new ArgumentOutOfRangeException(nameof(damageInflicted));
        if (casualties < 0) throw new ArgumentOutOfRangeException(nameof(casualties));
        if (overthrows < 0) throw new ArgumentOutOfRangeException(nameof(overthrows));
        if (timesHidden < 0) throw new ArgumentOutOfRangeException(nameof(timesHidden));
        CashEarned = cashEarned;
        CashSpent = cashSpent;
        DamageInflicted = damageInflicted;
        Casualties = casualties;
        Overthrows = overthrows;
        TimesHidden = timesHidden;
    }

    public long CashEarned { get; internal set; }
    public long CashSpent { get; internal set; }
    public int DamageInflicted { get; internal set; }
    public int Casualties { get; internal set; }
    public int Overthrows { get; internal set; }
    public int TimesHidden { get; internal set; }
}

/// <summary>
/// Authoritative headless state initialized from explicit mechanical data or the
/// statically recovered original city and headquarters generation path.
/// </summary>
public sealed partial class MatchState
{
    private readonly Dictionary<PlayerId, NotificationQueue> _notifications;
    private readonly Dictionary<PlayerId, long> _nextNotificationSequences;
    private readonly Dictionary<PlayerId, ComlinkInbox> _comlinkInboxes;
    private readonly Dictionary<PlayerId, MatchPlayerState> _playersById;
    private readonly MatchGangIndex _gangIndex;
    public MatchState(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors)
        : this(definitions, setup, players, sectors, (MatchRuntimeRestore?)null)
    {
    }
    internal MatchState(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors,
        MatchRuntimeRestore? restore)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
        if (setup.AllowsSparsePlayerIds)
            throw new ArgumentException(
                "Sparse local setup must be completed before authoritative state construction.",
                nameof(setup));
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(sectors);
        if (players.Count != setup.Players.Count)
            throw new ArgumentException("Player state count must match setup.", nameof(players));
        if (!players.Select(player => player.Id).SequenceEqual(setup.Players.Select(player => player.Id)))
            throw new ArgumentException("Player states must follow setup player order.", nameof(players));
        if (sectors.Count != MatchLimits.SectorCount)
            throw new ArgumentException($"A match must contain exactly {MatchLimits.SectorCount} sectors.", nameof(sectors));
        if (!sectors.Select(sector => sector.Id).SequenceEqual(Enumerable.Range(0, sectors.Count)))
            throw new ArgumentException("Sectors must be ordered and identified from 0 through 63.", nameof(sectors));
        // Indexing the rosters is how their gang identifiers are checked for collisions, so this
        // stands where the explicit uniqueness scan used to and costs one pass instead of three.
        _gangIndex = MatchGangIndex.ForRosters(players);

        ValidateDefinitionsAndCapacities(definitions, players, sectors);

        _eventView = _events.AsReadOnly();
        _phaseHashView = _phaseHashes.AsReadOnly();
        Players = players.ToArray();
        Sectors = sectors.ToArray();
        _playersById = Players.ToDictionary(player => player.Id);
        // Taking the roster over comes after every argument check, so a refused construction hands
        // the caller's players back unbound. It comes before RestoreRuntime, which looks gangs up.
        foreach (var player in Players) player.JoinMatch(_gangIndex);
        Coordinator = restore is null
            ? new TurnCoordinator(players.Count)
            : new TurnCoordinator(players.Count, restore.Turn, restore.Phase,
                restore.ExecutionPhase, restore.ActivePlayer);
        Random = restore is null
            ? new DeterministicRandom(setup.InitialSeed)
            : new DeterministicRandom(restore.RandomState, restore.RandomConsumptionCount);
        Commands = restore is null
            ? new TurnCommandQueue()
            : TurnCommandQueue.Restore(restore.Commands, restore.NextCommandSequence);
        AiStrategy = restore?.AiStrategy ?? AiStrategicState.MigrateLegacy(setup);
        AiPlanning = restore?.AiPlanning ?? AiPlanningState.Initialize(Players);
        _notifications = Players.ToDictionary(player => player.Id, _ => new NotificationQueue());
        _nextNotificationSequences = Players.ToDictionary(player => player.Id, _ => 0L);
        _comlinkInboxes = Players.ToDictionary(player => player.Id, _ => new ComlinkInbox());
        if (restore is not null) RestoreRuntime(restore);
    }
    internal MatchState(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors,
        DeterministicRandom initialRandom,
        AiStrategicState aiStrategy)
        : this(definitions, setup, players, sectors, new MatchRuntimeRestore(
            1, TurnPhase.Upkeep, null, null,
            initialRandom.State, initialRandom.ConsumptionCount,
            [], 0, [], 0,
            players.ToDictionary(player => player.Id,
                _ => (IReadOnlyList<GameNotification>)Array.Empty<GameNotification>()),
            players.ToDictionary(player => player.Id, _ => 0L),
            players.ToDictionary(player => player.Id,
                _ => new ComlinkInboxRestore([], 0, [])),
            [], null, aiStrategy, AiPlanningState.Initialize(players)))
    {
        ArgumentNullException.ThrowIfNull(initialRandom);
        ArgumentNullException.ThrowIfNull(aiStrategy);
    }
    public OriginalData Definitions { get; }
    public MatchSetup Setup { get; private set; }
    public IReadOnlyList<MatchPlayerState> Players { get; }
    public IReadOnlyList<MatchSectorState> Sectors { get; }
    public TurnCoordinator Coordinator { get; }
    public DeterministicRandom Random { get; }
    public TurnCommandQueue Commands { get; }
    public AiStrategicState AiStrategy { get; }
    public AiPlanningState AiPlanning { get; }
    public IReadOnlyList<CommandResolutionResult> LastPhaseResolutions { get; private set; } = [];
    public IReadOnlyList<PoliceAttackResolutionResult> LastPoliceAttackResolutions { get; private set; } = [];
    public IReadOnlyList<UpkeepResolutionResult> LastUpkeepResolutions { get; private set; } = [];
    public IReadOnlyList<HireResolutionResult> LastHireResolutions { get; private set; } = [];
    public MatchOutcome? Outcome { get; private set; }

    private void RestoreRuntime(MatchRuntimeRestore restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        if (restore.NextEventSequence != restore.Events.Count
            || restore.Events.Where((gameEvent, index) => gameEvent.Sequence != index).Any()
            || restore.Events.Any(gameEvent => !GameEventValidator.IsStructurallyValid(gameEvent)))
            throw new ArgumentException("Restored event history is invalid.", nameof(restore));
        foreach (var queued in restore.Commands)
        {
            var gang = FindGang(queued.Command.Gang);
            if (gang is null || gang.Owner != queued.Command.Player)
                throw new ArgumentException("A restored command does not belong to an existing gang.", nameof(restore));
        }
        if (!restore.Notifications.Keys.OrderBy(player => player.Value)
                .SequenceEqual(Players.Select(player => player.Id))
            || !restore.NextNotificationSequences.Keys.OrderBy(player => player.Value)
                .SequenceEqual(Players.Select(player => player.Id))
            || !restore.ComlinkInboxes.Keys.OrderBy(player => player.Value)
                .SequenceEqual(Players.Select(player => player.Id)))
            throw new ArgumentException("Restored notification players do not match the match setup.", nameof(restore));

        foreach (var gameEvent in restore.Events.OrderBy(gameEvent => gameEvent.Sequence))
            StoreEvent(gameEvent);
        _nextEventSequence = restore.NextEventSequence;
        RefreshQueuedCommands();
        foreach (var player in Players)
        {
            var next = restore.NextNotificationSequences[player.Id];
            var notifications = restore.Notifications[player.Id];
            if (!IsValidNotificationHistory(notifications, next, restore))
                throw new ArgumentException("Restored notification history is invalid.", nameof(restore));
            foreach (var notification in notifications) _notifications[player.Id].Enqueue(notification);
            _nextNotificationSequences[player.Id] = next;
            var inbox = restore.ComlinkInboxes[player.Id];
            if (inbox.Messages.Any(message => message.Turn > restore.Turn
                    || FindPlayer(message.Sender)?.Setup.Controller != PlayerController.Human))
                throw new ArgumentException("Restored Comlink senders are invalid.", nameof(restore));
            _comlinkInboxes[player.Id] = ComlinkInbox.Restore(
                inbox.Messages, inbox.NextSequence, inbox.ReadSequences,
                inbox.LegacyReadThroughSequence);
        }
        if (restore.PhaseHashes.Any(boundary =>
                boundary.Turn < 1
                || !Enum.IsDefined(boundary.Phase)
                || boundary.ExecutionPhase is { } phase && !Enum.IsDefined(phase)
                || (boundary.Phase == TurnPhase.Execution) != boundary.ExecutionPhase.HasValue
                || !MatchStateHasher.IsFingerprint(boundary.Fingerprint)))
            throw new ArgumentException("Restored phase hash history is invalid.", nameof(restore));
        foreach (var boundary in restore.PhaseHashes) StorePhaseHash(boundary);
        RestoreOutcome(restore);
    }
    public bool CanPlayerDetectGang(PlayerId observer, GangId targetGang)
    {
        var player = FindPlayer(observer) ?? throw new ArgumentOutOfRangeException(nameof(observer));
        var target = FindGang(targetGang) ?? throw new ArgumentOutOfRangeException(nameof(targetGang));
        if (target.Owner == observer) return true;
        if (OriginalSetupNameRules.EnablesOmniscience(player.Setup.Name)) return true;
        var observers = player.Gangs.Where(gang => gang.IsActive && gang.SectorId == target.SectorId).ToArray();
        if (observers.Length == 0) return false;
        var detection = ManualRules.SectorDetectionStrength(
            observers.Select(gang => EffectiveStatisticsCalculator.ForGang(this, gang).Detect));
        return detection >= EffectiveStatisticsCalculator.ForGang(this, target).Stealth;
    }
    public IReadOnlyList<GameNotification> NotificationsFor(PlayerId player) => GetNotificationQueue(player).Items;
    public bool TryDismissNotification(PlayerId player, out GameNotification? notification) =>
        GetNotificationQueue(player).TryDequeue(out notification);

    public TurnTransition FinishUpkeep()
    {
        if (Outcome is not null)
            throw new InvalidOperationException("The match has ended and cannot advance another turn.");
        NormalizeRecurringCommands();
        foreach (var gang in Players.SelectMany(player => player.Gangs))
        {
            gang.Hidden = gang.QueuedCommand?.Command.Action == GangAction.Hide;
            gang.HiredThisTurn = false;
        }
        CrackdownResolver.ResolveUpkeep(this);
        LastUpkeepResolutions = Coordinator.Turn == 1 ? [] : EconomyResolver.ResolveUpkeep(this);
        SectorBenefitResolver.ActivatePending(this);
        ToleranceResolver.RebuildBeforePlanning(this);
        return CaptureBoundary(Coordinator.FinishUpkeep());
    }
    public TurnTransition FinishCommand(PlayerId player)
    {
        var transition = Coordinator.FinishCommand(player);
        if (transition.Phase == TurnPhase.Execution
            && Setup.AiMentality != AiDifficulty.HomicidalManiac)
            AiStrategy.RecoverForResolution();
        return CaptureBoundary(transition);
    }
    public TurnTransition FinishExecutionPhase()
    {
        if (Coordinator.Phase != TurnPhase.Execution)
            throw new InvalidOperationException($"Cannot complete Execution while in {Coordinator.Phase}.");
        var phase = Coordinator.ExecutionPhase
            ?? throw new InvalidOperationException("Execution subphase is missing.");
        LastPoliceAttackResolutions = [];
        var commands = Commands.ForPhase(phase);
        var unsupported = commands.FirstOrDefault(command => !CommandResolver.IsSupported(command.Command.Action));
        if (unsupported is not null)
        {
            LastPhaseResolutions = [];
            throw new NotSupportedException($"{unsupported.Command.Action} resolution has not been implemented.");
        }

        if (phase == ExecutionPhase.Combat)
        {
            var combat = CommandResolver.ResolveCombatPhase(this, commands);
            LastPhaseResolutions = combat.Commands;
            LastPoliceAttackResolutions = combat.PoliceAttacks;
            CrackdownResolver.FinishCombat(this);
        }
        else
        {
            LastPhaseResolutions = CommandResolver.ResolvePhase(this, commands);
        }
        // The native resolver rolls Chaos and creates Crackdowns here so the resulting police
        // participate in Combat; its stored successes are paid at the later Chaos boundary.
        if (phase == ExecutionPhase.Instant)
            _ = CommandResolver.PrepareChaosPhase(this, Commands.ForPhase(ExecutionPhase.Chaos));
        foreach (var result in LastPhaseResolutions.Where(ShouldStopResolvedCommand))
        {
            Commands.Cancel(result.Command.Gang);
            if (FindGang(result.Command.Gang) is { } gang) gang.QueuedCommand = null;
        }
        var transition = Coordinator.FinishExecutionPhase();
        if (phase == TurnStructure.ExecutionOrder[^1])
        {
            Commands.FinishExecution();
            RefreshQueuedCommands();
        }
        return CaptureBoundary(transition);
    }

    /// <summary>Points every gang at its entry in the command queue, or at nothing.</summary>
    private void RefreshQueuedCommands()
    {
        foreach (var gang in Players.SelectMany(player => player.Gangs))
            gang.QueuedCommand = Commands.TryGet(gang.Id, out var queued) ? queued : null;
    }

    private void CancelQueuedCommand(MatchGangState gang)
    {
        Commands.Cancel(gang.Id);
        gang.QueuedCommand = null;
    }

    public TurnTransition FinishHire(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Hire || Coordinator.ActivePlayer != player)
            return CaptureBoundary(Coordinator.FinishHire(player));
        var state = FindPlayer(player) ?? throw new ArgumentOutOfRangeException(nameof(player));
        LastHireResolutions = HireResolver.Resolve(this, state);
        var transition = Coordinator.FinishHire(player);
        return CaptureBoundary(transition);
    }

    public TurnTransition FinishPlayerElimination()
    {
        if (Coordinator.Phase != TurnPhase.PlayerElimination)
            return CaptureBoundary(Coordinator.FinishPlayerElimination());
        ResolvePlayerEliminations();
        AwardBigManPoints();
        if (Outcome is null && MatchOutcomeEvaluator.Evaluate(this) is { } outcome)
        {
            Outcome = MatchOutcomeValidator.Freeze(outcome);
            AppendMatchEndedEvent(Outcome);
        }
        return CaptureBoundary(Coordinator.FinishPlayerElimination());
    }

    public HireSubmissionResult QueueHire(PlayerId playerId, short gangDefinitionId, int targetSectorId)
    {
        var validation = HireRules.Validate(this, playerId, gangDefinitionId, targetSectorId);
        if (!validation.IsValid) return new HireSubmissionResult(validation);

        var player = FindPlayer(playerId)!;
        var definition = Definitions.Gang(gangDefinitionId);
        var cost = HireRules.InitialCost(definition);
        var offerSlot = player.FindHireOfferSlot(gangDefinitionId);
        ClearHireAction(player);
        var pending = new PendingHireState(
            gangDefinitionId, targetSectorId, offerSlot, InitialCostPaid: false);
        player.AddPendingHire(pending);
        var gameEvent = AppendHireEvent(GameEventKind.HireQueued, playerId,
            new HireResolutionDetails(gangDefinitionId, targetSectorId, cost));
        return new HireSubmissionResult(validation, pending, gameEvent);
    }

    internal HireSubmissionResult QueueHireLegacyImmediatePayment(
        PlayerId playerId,
        short gangDefinitionId,
        int targetSectorId)
    {
        var validation = HireRules.ValidateLegacyImmediatePayment(
            this, playerId, gangDefinitionId, targetSectorId);
        if (!validation.IsValid) return new HireSubmissionResult(validation);

        var player = FindPlayer(playerId)!;
        var definition = Definitions.Gang(gangDefinitionId);
        var cost = HireRules.InitialCost(definition);
        var offerSlot = player.FindHireOfferSlot(gangDefinitionId);
        var pending = new PendingHireState(
            gangDefinitionId, targetSectorId, offerSlot, InitialCostPaid: true);
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        player.AddPendingHire(pending);
        var gameEvent = AppendHireEvent(GameEventKind.HireQueued, playerId,
            new HireResolutionDetails(gangDefinitionId, targetSectorId, cost));
        return new HireSubmissionResult(validation, pending, gameEvent);
    }

    public IReadOnlyList<short> PrepareHireOffers(PlayerId playerId)
    {
        if (Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire)
            || Coordinator.ActivePlayer != playerId)
            throw new InvalidOperationException("Hire offers can only be prepared for the active planning player.");
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Status != PlayerStatus.Active)
            throw new InvalidOperationException("An eliminated player cannot prepare hire offers.");
        HireResolver.FillOffers(this, player);
        return player.HirePool;
    }

    public HireOfferSnubResult SnubHireOffer(PlayerId playerId, short gangDefinitionId)
    {
        var validation = HireRules.ValidateSnub(this, playerId, gangDefinitionId);
        if (!validation.IsValid) return new HireOfferSnubResult(validation);
        var player = FindPlayer(playerId)!;
        var offerSlot = player.FindHireOfferSlot(gangDefinitionId);
        var selectedPending = player.PendingHires.SingleOrDefault();
        var cancelsPending = selectedPending is not null
            && (selectedPending.OfferSlot == offerSlot
                || selectedPending.GangDefinitionId == gangDefinitionId);
        var cancelsSnub = player.SnubbedHireOfferSlot == offerSlot
            || player.SnubbedHireOffer == gangDefinitionId;
        ClearHireAction(player);
        if (cancelsPending || cancelsSnub)
            return new HireOfferSnubResult(validation);

        player.MarkHireOfferSnubbed(gangDefinitionId, offerSlot);
        var gameEvent = AppendHireOfferEvent(
            GameEventKind.HireOfferSnubbed, playerId,
            new HireOfferDetails(gangDefinitionId, null));
        return new HireOfferSnubResult(validation, gangDefinitionId, gameEvent);
    }

    private void ClearHireAction(MatchPlayerState player)
    {
        foreach (var pending in player.PendingHires)
        {
            if (!pending.InitialCostPaid) continue;
            var definition = Definitions.Gang(pending.GangDefinitionId);
            var cost = HireRules.InitialCost(definition);
            player.Cash += cost;
            player.Statistics.CashSpent -= cost;
        }
        player.ClearPendingHires();
        player.ClearSnubbedHireOffer();
    }

    public CommandSubmissionResult Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = CommandValidator.Validate(this, command);
        if (!validation.IsValid)
            return new CommandSubmissionResult(validation, null);

        var replaced = Commands.TryGet(command.Gang, out _);
        var queued = Commands.Set(command);
        var gang = FindGang(command.Gang)!;
        gang.QueuedCommand = queued;
        gang.Hidden = command.Action == GangAction.Hide;
        RecordAiPlannedAction(command);
        var gameEvent = AppendEvent(replaced ? GameEventKind.CommandReplaced : GameEventKind.CommandQueued, command);
        return new CommandSubmissionResult(validation, gameEvent);
    }

    public CommandSubmissionResult Cancel(PlayerId player, GangId gangId)
    {
        var command = new GameCommand(player, gangId, GangAction.None, CommandTarget.None);
        var validation = CommandValidator.ValidateCancellation(this, player, gangId);
        if (!validation.IsValid)
            return new CommandSubmissionResult(validation, null);
        if (!Commands.Cancel(gangId))
            return new CommandSubmissionResult(CommandValidation.Reject(CommandValidationCode.CommandNotQueued), null);

        var gang = FindGang(gangId)!;
        gang.QueuedCommand = null;
        gang.Hidden = false;
        RecordAiPlannedAction(command);
        return new CommandSubmissionResult(validation, AppendEvent(GameEventKind.CommandCancelled, command));
    }

    private GameEvent AppendEvent(GameEventKind kind, GameCommand command)
    {
        var gameEvent = new GameEvent(
            _nextEventSequence++,
            Coordinator.Turn,
            Coordinator.Phase,
            Coordinator.ExecutionPhase,
            kind,
            command.Player,
            command.Gang,
            command.Action,
            command.Target,
            command.SecondaryTarget,
            command.TertiaryTarget,
            command.QuaternaryTarget);
        return StoreEvent(gameEvent);
    }

    internal GameEvent AppendResolutionEvent(
        GameEventKind kind,
        GameCommand command,
        CommandResolutionDetails resolution,
        ExecutionPhase? executionPhase = null)
    {
        if (kind is not (GameEventKind.CommandResolved or GameEventKind.CommandFailed))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var gameEvent = new GameEvent(
            _nextEventSequence++,
            Coordinator.Turn,
            Coordinator.Phase,
            executionPhase ?? Coordinator.ExecutionPhase,
            kind,
            command.Player,
            command.Gang,
            command.Action,
            command.Target,
            command.SecondaryTarget,
            command.TertiaryTarget,
            command.QuaternaryTarget,
            resolution);
        return StoreEvent(gameEvent);
    }

    /// <summary>
    /// Appends an event no gang command produced: action None, the one detail record the caller
    /// names, and a Sector target when <paramref name="sectorId"/> is given. The sequence number
    /// is taken before the sector target is built, as each builder did on its own.
    /// </summary>
    private GameEvent AppendSystemEvent(
        GameEventKind kind,
        PlayerId player,
        GangId? gang = null,
        int? sectorId = null,
        EconomyResolutionDetails? economy = null,
        HireResolutionDetails? hire = null,
        HireOfferDetails? hireOffer = null,
        EliminationDetails? elimination = null,
        PoliceAttackResolutionDetails? policeAttack = null,
        BigManPointDetails? bigManPoints = null,
        MatchOutcomeDetails? matchOutcome = null) =>
        StoreEvent(new GameEvent(
            _nextEventSequence++, Coordinator.Turn, Coordinator.Phase,
            Coordinator.ExecutionPhase, kind, player, gang, GangAction.None,
            sectorId is { } sector ? CommandTarget.Sector(sector) : CommandTarget.None,
            Economy: economy, Hire: hire, HireOffer: hireOffer, Elimination: elimination,
            PoliceAttack: policeAttack, BigManPoints: bigManPoints, MatchOutcome: matchOutcome));

    internal GameEvent AppendUpkeepEvent(PlayerId player, EconomyResolutionDetails economy) =>
        AppendSystemEvent(GameEventKind.UpkeepResolved, player, economy: economy);

    internal GangId NextGangId() => new(
        Players.SelectMany(player => player.Gangs).Select(gang => gang.Id.Value).DefaultIfEmpty(-1).Max() + 1);

    internal GameEvent AppendHireEvent(GameEventKind kind, PlayerId player, HireResolutionDetails hire)
    {
        if (kind is not (GameEventKind.HireQueued or GameEventKind.HireResolved or GameEventKind.HireFailed))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return AppendSystemEvent(kind, player, hire.Gang, hire.SectorId, hire: hire);
    }

    internal GameEvent AppendHireOfferEvent(
        GameEventKind kind,
        PlayerId player,
        HireOfferDetails hireOffer)
    {
        if (kind is not (GameEventKind.HireOfferSnubbed or GameEventKind.HireOfferRefilled))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return AppendSystemEvent(kind, player, hireOffer: hireOffer);
    }

    internal GameEvent AppendPoliceAttackEvent(
        PlayerId player,
        GangId gang,
        PoliceAttackResolutionDetails policeAttack) =>
        AppendSystemEvent(
            GameEventKind.PoliceAttackResolved, player, gang, policeAttack.SectorId,
            policeAttack: policeAttack);

    private void ResolvePlayerEliminations()
    {
        var eliminated = Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Where(player => Setup.Scenario == ScenarioId.Eliminate
                ? player.Gangs.All(gang => !gang.IsActive || gang.DefinitionId != 0)
                : player.Gangs.All(gang => !gang.IsActive)
                  && Sectors.All(sector => sector.Owner != player.Id))
            .OrderBy(player => player.Id.Value)
            .ToArray();

        foreach (var player in eliminated)
        {
            player.Status = PlayerStatus.Eliminated;
            player.ClearPendingHires();
            if (Setup.Scenario == ScenarioId.Eliminate)
            {
                foreach (var gang in player.Gangs)
                {
                    CancelQueuedCommand(gang);
                    if (gang.IsActive) RetireGangForEliminate(gang);
                }
                // Neutralize rather than clearing Owner: every other way of losing a sector runs
                // ResetInfluencedSites, which restores Resistance on all three sites. Clearing the
                // owner on its own stranded a site whose Resistance had just reached zero but whose
                // ownership had not yet activated at the next upkeep, because ActivatePending skips
                // ownerless sectors. The site then stayed at Resistance 0 with no influencer, so the
                // next player to take the sector could neither influence it nor collect from it.
                foreach (var sector in Sectors.Where(sector => sector.Owner == player.Id))
                    SectorControlResolver.Neutralize(this, sector);
            }
            foreach (var sector in Sectors)
            foreach (var site in sector.Sites.Where(site => site.InfluencedBy == player.Id))
            {
                var definition = Definitions.Site(site.DefinitionId);
                // The sector's Tolerance drops the site's part at the next rebuild (RULE-SITE-001).
                // As SectorControlResolver.ResetInfluencedSites does when a sector changes hands:
                // the site stops being influenced, so the Support it granted stops counting.
                player.Support -= definition.Support;
                site.InfluencedBy = null;
                site.Resistance = definition.Resistance;
            }

            var details = new EliminationDetails(
                player.Id,
                Players.Count(candidate => candidate.Status == PlayerStatus.Active));
            var gameEvent = AppendEliminationEvent(player.Id, details);
            foreach (var recipient in Players.Where(candidate => candidate.Status == PlayerStatus.Active || candidate.Id == player.Id))
                QueueNotification(recipient.Id, GameNotificationKind.Elimination,
                    relatedEventSequence: gameEvent.Sequence);
        }
    }

    private void AwardBigManPoints()
    {
        if (Setup.Scenario != ScenarioId.BigMan) return;
        int[] centralSectors = [27, 28, 35, 36];
        foreach (var player in Players.Where(player => player.Status == PlayerStatus.Active).OrderBy(player => player.Id.Value))
        {
            var controlled = centralSectors.Count(sectorId => Sectors[sectorId].Owner == player.Id);
            if (controlled == 0) continue;
            var previous = player.BigManPoints;
            player.BigManPoints = checked(previous + controlled);
            var details = new BigManPointDetails(previous, controlled, player.BigManPoints);
            var gameEvent = AppendBigManPointsEvent(player.Id, details);
            QueueNotification(player.Id, GameNotificationKind.Objective,
                relatedEventSequence: gameEvent.Sequence);
        }
    }

    private static void RetireGangForEliminate(MatchGangState gang)
    {
        // Native Eliminate cleanup writes only inactive sector 100. Force zero is this
        // model's inactive marker; retain equipment so the retired record stays inspectable.
        gang.Force = 0;
        gang.Hidden = false;
    }

    private GameEvent AppendEliminationEvent(PlayerId player, EliminationDetails elimination) =>
        AppendSystemEvent(GameEventKind.PlayerEliminated, player, elimination: elimination);

    private GameEvent AppendBigManPointsEvent(PlayerId player, BigManPointDetails bigManPoints) =>
        AppendSystemEvent(GameEventKind.BigManPointsAwarded, player, bigManPoints: bigManPoints);

    private GameEvent AppendMatchEndedEvent(MatchOutcome outcome)
    {
        var details = new MatchOutcomeDetails(
            outcome.Scenario, outcome.Reason, outcome.Turn, outcome.Winners,
            outcome.Standings, outcome.Awards);
        // The event names a player, and a match nobody won has none to name: the first standing
        // stands in for it, which is deterministic and the same on every client.
        var subject = outcome.Winners.Count > 0
            ? outcome.Winners[0]
            : outcome.Standings[0].Player;
        var gameEvent = AppendSystemEvent(
            GameEventKind.MatchEnded, subject, matchOutcome: details);
        foreach (var player in Players)
            QueueNotification(player.Id, GameNotificationKind.Objective,
                relatedEventSequence: gameEvent.Sequence);
        return gameEvent;
    }

    internal GameNotification QueueNotification(
        PlayerId player,
        GameNotificationKind kind,
        GangId? gang = null,
        int? sectorId = null,
        long? relatedEventSequence = null,
        ExecutionPhase? executionPhase = null)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount) throw new ArgumentOutOfRangeException(nameof(sectorId));
        var queue = GetNotificationQueue(player);
        var notification = new GameNotification(
            _nextNotificationSequences[player]++, Coordinator.Turn, Coordinator.Phase,
            executionPhase ?? Coordinator.ExecutionPhase, kind, gang, sectorId, relatedEventSequence);
        queue.Enqueue(notification);
        return notification;
    }

    internal long NextNotificationSequence(PlayerId player) =>
        _nextNotificationSequences.TryGetValue(player, out var sequence)
            ? sequence
            : throw new ArgumentOutOfRangeException(nameof(player));
    private NotificationQueue GetNotificationQueue(PlayerId player) =>
        _notifications.TryGetValue(player, out var queue)
            ? queue
            : throw new ArgumentOutOfRangeException(nameof(player));

}

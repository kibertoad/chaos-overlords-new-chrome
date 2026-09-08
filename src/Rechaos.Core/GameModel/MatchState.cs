using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public enum PlayerController : byte
{
    Human,
    Computer
}

public enum PlayerStatus : byte
{
    Active,
    Eliminated
}

public sealed record MatchPlayerSetup(PlayerId Id, string Name, PlayerController Controller);

public sealed class MatchSetup
{
    public MatchSetup(
        ScenarioId scenario,
        GameDuration duration,
        int initialSeed,
        IReadOnlyList<MatchPlayerSetup> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count is < 1 or > MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(players));
        if (!players.Select(player => player.Id.Value).SequenceEqual(Enumerable.Range(0, players.Count)))
            throw new ArgumentException("Player identifiers must be ordered and contiguous from zero.", nameof(players));
        if (players.Any(player => string.IsNullOrWhiteSpace(player.Name)))
            throw new ArgumentException("Player names cannot be blank.", nameof(players));

        Scenario = scenario;
        Duration = duration;
        InitialSeed = initialSeed;
        Players = players.ToArray();
    }

    public ScenarioId Scenario { get; }
    public GameDuration Duration { get; }
    public int InitialSeed { get; }
    public IReadOnlyList<MatchPlayerSetup> Players { get; }
}

public sealed class MatchPlayerState
{
    private readonly List<MatchGangState> _gangs;
    private readonly List<short> _hirePool;
    private readonly List<PendingHireState> _pendingHires;
    private readonly Dictionary<short, int> _researchProgress;
    private readonly HashSet<short> _researchedItems;
    private readonly Dictionary<short, int> _inventory;

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
        PlayerStatus status = PlayerStatus.Active)
    {
        if (cash < 0) throw new ArgumentOutOfRangeException(nameof(cash));
        if (bigManPoints < 0) throw new ArgumentOutOfRangeException(nameof(bigManPoints));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _gangs = gangs?.ToList() ?? [];
        _hirePool = hirePool?.ToList() ?? [];
        _pendingHires = pendingHires?.ToList() ?? [];
        _researchProgress = researchProgress?.ToDictionary() ?? [];
        _researchedItems = researchedItems is null ? [] : new HashSet<short>(researchedItems);
        _inventory = inventory?.ToDictionary() ?? [];
        Cash = cash;
        Support = support;
        BigManPoints = bigManPoints;
        Status = status;
    }

    public MatchPlayerSetup Setup { get; }
    public PlayerId Id => Setup.Id;
    public PlayerStatus Status { get; internal set; } = PlayerStatus.Active;
    public int Cash { get; internal set; }
    public int Support { get; internal set; }
    public int BigManPoints { get; internal set; }
    public IReadOnlyList<MatchGangState> Gangs => _gangs;
    public IReadOnlyList<short> HirePool => _hirePool;
    public IReadOnlyList<PendingHireState> PendingHires => _pendingHires;
    public IReadOnlyDictionary<short, int> ResearchProgress => _researchProgress;
    public IReadOnlySet<short> ResearchedItems => _researchedItems;
    public IReadOnlyDictionary<short, int> Inventory => _inventory;
    public MatchStatistics Statistics { get; } = new();

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

    internal void AddGang(MatchGangState gang) => _gangs.Add(gang);
    internal void AddPendingHire(PendingHireState hire) => _pendingHires.Add(hire);
    internal void ClearPendingHires() => _pendingHires.Clear();
    internal bool RemoveHireOffer(short gangDefinitionId) => _hirePool.Remove(gangDefinitionId);
    internal void AddHireOffer(short gangDefinitionId) => _hirePool.Add(gangDefinitionId);

    private static void ValidateResearchItem(OriginalData definitions, short itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= definitions.Items.Count || definitions.Items[itemIndex].Type == 99)
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

public sealed record PendingHireState(short GangDefinitionId, int TargetSectorId);

public sealed class MatchSectorState
{
    public MatchSectorState(
        int id,
        IReadOnlyList<MatchSiteState> sites,
        PlayerId? owner = null,
        int tolerance = ManualRules.MinimumTolerance,
        int chaos = 0,
        bool crackdownActive = false)
    {
        if (id is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(sites);
        if (sites.Count != MatchLimits.SitesPerSector)
            throw new ArgumentException($"A sector must contain exactly {MatchLimits.SitesPerSector} sites.", nameof(sites));
        if (sites.Select(site => site.Slot).Order().SequenceEqual(Enumerable.Range(0, MatchLimits.SitesPerSector)) is false)
            throw new ArgumentException("Site slots must be exactly 0, 1, and 2.", nameof(sites));
        if (tolerance is < ManualRules.MinimumTolerance or > ManualRules.MaximumTolerance)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (chaos < 0) throw new ArgumentOutOfRangeException(nameof(chaos));
        Id = id;
        Sites = sites.OrderBy(site => site.Slot).ToArray();
        Owner = owner;
        Tolerance = tolerance;
        Chaos = chaos;
        CrackdownActive = crackdownActive;
    }

    public int Id { get; }
    public IReadOnlyList<MatchSiteState> Sites { get; }
    public PlayerId? Owner { get; internal set; }
    public int Tolerance { get; internal set; }
    public int Chaos { get; internal set; }
    public bool CrackdownActive { get; internal set; }
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
    public long CashEarned { get; internal set; }
    public long CashSpent { get; internal set; }
    public int DamageInflicted { get; internal set; }
    public int Casualties { get; internal set; }
    public int Overthrows { get; internal set; }
}

/// <summary>
/// Headless compatibility state. It is initialized from explicit mechanical data;
/// exact original city and player placement remain a separate M1 research task.
/// </summary>
public sealed class MatchState
{
    private readonly List<GameEvent> _events = [];
    private readonly Dictionary<PlayerId, NotificationQueue> _notifications;
    private readonly Dictionary<PlayerId, long> _nextNotificationSequences;
    private readonly List<PhaseBoundaryHash> _phaseHashes = [];
    private long _nextEventSequence;

    public MatchState(
        OriginalData definitions,
        MatchSetup setup,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
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
        if (players.SelectMany(player => player.Gangs).Select(gang => gang.Id).Distinct().Count()
            != players.Sum(player => player.Gangs.Count))
            throw new ArgumentException("Gang identifiers must be unique across the match.", nameof(players));

        ValidateDefinitionsAndCapacities(definitions, players, sectors);

        Players = players.ToArray();
        Sectors = sectors.ToArray();
        Coordinator = new TurnCoordinator(players.Count);
        Random = new DeterministicRandom(setup.InitialSeed);
        _notifications = Players.ToDictionary(player => player.Id, _ => new NotificationQueue());
        _nextNotificationSequences = Players.ToDictionary(player => player.Id, _ => 0L);
    }

    public OriginalData Definitions { get; }
    public MatchSetup Setup { get; }
    public IReadOnlyList<MatchPlayerState> Players { get; }
    public IReadOnlyList<MatchSectorState> Sectors { get; }
    public TurnCoordinator Coordinator { get; }
    public DeterministicRandom Random { get; }
    public TurnCommandQueue Commands { get; } = new();
    public IReadOnlyList<GameEvent> Events => _events;
    public IReadOnlyList<PhaseBoundaryHash> PhaseHashes => _phaseHashes;
    public IReadOnlyList<CommandResolutionResult> LastPhaseResolutions { get; private set; } = [];
    public IReadOnlyList<UpkeepResolutionResult> LastUpkeepResolutions { get; private set; } = [];
    public IReadOnlyList<HireResolutionResult> LastHireResolutions { get; private set; } = [];
    internal long NextEventSequence => _nextEventSequence;

    public MatchPlayerState? FindPlayer(PlayerId id) => Players.SingleOrDefault(player => player.Id == id);
    public MatchGangState? FindGang(GangId id) => Players.SelectMany(player => player.Gangs).SingleOrDefault(gang => gang.Id == id);
    public MatchSiteState? FindSite(int id) => id is >= 0 and < MatchLimits.SiteCount
        ? Sectors[id / MatchLimits.SitesPerSector].Sites[id % MatchLimits.SitesPerSector]
        : null;
    public bool CanPlayerDetectGang(PlayerId observer, GangId targetGang)
    {
        var player = FindPlayer(observer) ?? throw new ArgumentOutOfRangeException(nameof(observer));
        var target = FindGang(targetGang) ?? throw new ArgumentOutOfRangeException(nameof(targetGang));
        if (target.Owner == observer) return true;
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
        foreach (var gang in Players.SelectMany(player => player.Gangs))
        {
            gang.Hidden = false;
            gang.HiredThisTurn = false;
        }
        LastUpkeepResolutions = EconomyResolver.ResolveUpkeep(this);
        return CaptureBoundary(Coordinator.FinishUpkeep());
    }
    public TurnTransition FinishCommand(PlayerId player) => CaptureBoundary(Coordinator.FinishCommand(player));
    public TurnTransition FinishExecutionPhase()
    {
        if (Coordinator.Phase != TurnPhase.Execution)
            throw new InvalidOperationException($"Cannot complete Execution while in {Coordinator.Phase}.");
        var phase = Coordinator.ExecutionPhase
            ?? throw new InvalidOperationException("Execution subphase is missing.");
        var commands = Commands.ForPhase(phase);
        var unsupported = commands.FirstOrDefault(command => !CommandResolver.IsSupported(command.Command.Action));
        if (unsupported is not null)
        {
            LastPhaseResolutions = [];
            throw new NotSupportedException($"{unsupported.Command.Action} resolution has not been implemented.");
        }

        LastPhaseResolutions = CommandResolver.ResolvePhase(this, commands);
        var transition = Coordinator.FinishExecutionPhase();
        if (phase == TurnStructure.ExecutionOrder[^1])
        {
            Commands.FinishExecution();
            foreach (var gang in Players.SelectMany(player => player.Gangs))
                gang.QueuedCommand = Commands.TryGet(gang.Id, out var queued) ? queued : null;
        }
        return CaptureBoundary(transition);
    }
    public TurnTransition FinishHire(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Hire || Coordinator.ActivePlayer != player)
            return CaptureBoundary(Coordinator.FinishHire(player));
        var state = FindPlayer(player) ?? throw new ArgumentOutOfRangeException(nameof(player));
        LastHireResolutions = HireResolver.Resolve(this, state);
        return CaptureBoundary(Coordinator.FinishHire(player));
    }

    public TurnTransition FinishPlayerElimination()
    {
        if (Coordinator.Phase != TurnPhase.PlayerElimination)
            return CaptureBoundary(Coordinator.FinishPlayerElimination());
        ResolvePlayerEliminations();
        return CaptureBoundary(Coordinator.FinishPlayerElimination());
    }

    public HireSubmissionResult QueueHire(PlayerId playerId, short gangDefinitionId, int targetSectorId)
    {
        var validation = HireRules.Validate(this, playerId, gangDefinitionId, targetSectorId);
        if (!validation.IsValid) return new HireSubmissionResult(validation);

        var player = FindPlayer(playerId)!;
        var definition = Definitions.Gangs.Single(item => item.Id == gangDefinitionId);
        var cost = HireRules.InitialCost(definition);
        var pending = new PendingHireState(gangDefinitionId, targetSectorId);
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        player.RemoveHireOffer(gangDefinitionId);
        player.AddPendingHire(pending);
        var gameEvent = AppendHireEvent(GameEventKind.HireQueued, playerId,
            new HireResolutionDetails(gangDefinitionId, targetSectorId, cost));
        return new HireSubmissionResult(validation, pending, gameEvent);
    }

    public CommandSubmissionResult Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = CommandValidator.Validate(this, command);
        if (!validation.IsValid)
            return new CommandSubmissionResult(validation, null);

        var replaced = Commands.TryGet(command.Gang, out _);
        var queued = Commands.Set(command);
        FindGang(command.Gang)!.QueuedCommand = queued;
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

        FindGang(gangId)!.QueuedCommand = null;
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
            command.SecondaryTarget);
        _events.Add(gameEvent);
        return gameEvent;
    }

    internal GameEvent AppendResolutionEvent(
        GameEventKind kind,
        GameCommand command,
        CommandResolutionDetails resolution)
    {
        if (kind is not (GameEventKind.CommandResolved or GameEventKind.CommandFailed))
            throw new ArgumentOutOfRangeException(nameof(kind));
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
            resolution);
        _events.Add(gameEvent);
        return gameEvent;
    }

    internal GameEvent AppendUpkeepEvent(PlayerId player, EconomyResolutionDetails economy)
    {
        var gameEvent = new GameEvent(
            _nextEventSequence++,
            Coordinator.Turn,
            Coordinator.Phase,
            Coordinator.ExecutionPhase,
            GameEventKind.UpkeepResolved,
            player,
            null,
            GangAction.None,
            CommandTarget.None,
            Economy: economy);
        _events.Add(gameEvent);
        return gameEvent;
    }

    internal GangId NextGangId() => new(
        Players.SelectMany(player => player.Gangs).Select(gang => gang.Id.Value).DefaultIfEmpty(-1).Max() + 1);

    internal GameEvent AppendHireEvent(GameEventKind kind, PlayerId player, HireResolutionDetails hire)
    {
        if (kind is not (GameEventKind.HireQueued or GameEventKind.HireResolved))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var gameEvent = new GameEvent(
            _nextEventSequence++, Coordinator.Turn, Coordinator.Phase,
            Coordinator.ExecutionPhase, kind, player, hire.Gang,
            GangAction.None, CommandTarget.Sector(hire.SectorId), Hire: hire);
        _events.Add(gameEvent);
        return gameEvent;
    }

    private void ResolvePlayerEliminations()
    {
        var eliminated = Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Where(player => player.Gangs.All(gang => !gang.IsActive))
            .Where(player => Sectors.All(sector => sector.Owner != player.Id))
            .OrderBy(player => player.Id.Value)
            .ToArray();

        foreach (var player in eliminated)
        {
            player.Status = PlayerStatus.Eliminated;
            player.ClearPendingHires();
            foreach (var sector in Sectors)
            foreach (var site in sector.Sites.Where(site => site.InfluencedBy == player.Id))
            {
                site.InfluencedBy = null;
                site.Resistance = Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Resistance;
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

    private GameEvent AppendEliminationEvent(PlayerId player, EliminationDetails elimination)
    {
        var gameEvent = new GameEvent(
            _nextEventSequence++, Coordinator.Turn, Coordinator.Phase,
            Coordinator.ExecutionPhase, GameEventKind.PlayerEliminated, player,
            null, GangAction.None, CommandTarget.None, Elimination: elimination);
        _events.Add(gameEvent);
        return gameEvent;
    }

    internal GameNotification QueueNotification(
        PlayerId player,
        GameNotificationKind kind,
        GangId? gang = null,
        int? sectorId = null,
        long? relatedEventSequence = null)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount) throw new ArgumentOutOfRangeException(nameof(sectorId));
        var queue = GetNotificationQueue(player);
        var notification = new GameNotification(
            _nextNotificationSequences[player]++, Coordinator.Turn, Coordinator.Phase,
            Coordinator.ExecutionPhase, kind, gang, sectorId, relatedEventSequence);
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

    private TurnTransition CaptureBoundary(TurnTransition transition)
    {
        _phaseHashes.Add(new PhaseBoundaryHash(
            Coordinator.Turn,
            Coordinator.Phase,
            Coordinator.ExecutionPhase,
            MatchStateHasher.ComputeSha256(this)));
        return transition;
    }

    private static void ValidateDefinitionsAndCapacities(
        OriginalData definitions,
        IReadOnlyList<MatchPlayerState> players,
        IReadOnlyList<MatchSectorState> sectors)
    {
        foreach (var player in players)
        {
            if (player.Gangs.Count > MatchLimits.GangsPerPlayer)
                throw new ArgumentException($"Player {player.Id} exceeds gang capacity.", nameof(players));
            if (player.HirePool.Count > MatchLimits.HireOffersPerPlayer)
                throw new ArgumentException($"Player {player.Id} exceeds hire-pool capacity.", nameof(players));
            if (player.HirePool.Count != player.HirePool.Distinct().Count())
                throw new ArgumentException($"Player {player.Id} has duplicate hire offers.", nameof(players));
            if (player.HirePool.Any(id => id == 0 || !definitions.Gangs.Any(definition => definition.Id == id)))
                throw new ArgumentException($"Player {player.Id} has an invalid hire offer.", nameof(players));
            if (player.PendingHires.Count > 1)
                throw new ArgumentException($"Player {player.Id} has more than one pending hire.", nameof(players));
            if (player.PendingHires.Any(hire => !player.HirePool.Contains(hire.GangDefinitionId) &&
                    !definitions.Gangs.Any(definition => definition.Id == hire.GangDefinitionId)))
                throw new ArgumentException($"Player {player.Id} has an invalid pending hire.", nameof(players));
            if (player.PendingHires.Any(hire => hire.TargetSectorId is < 0 or >= MatchLimits.SectorCount))
                throw new ArgumentException($"Player {player.Id} has an invalid pending-hire sector.", nameof(players));
            if (player.Gangs.Any(gang => gang.Owner != player.Id))
                throw new ArgumentException($"Player {player.Id} contains a gang owned by another player.", nameof(players));
            if (player.Gangs.Any(gang => !definitions.Gangs.Any(definition => definition.Id == gang.DefinitionId)))
                throw new ArgumentException($"Player {player.Id} contains an unknown gang definition.", nameof(players));
            if (player.Gangs.Any(gang => EquippedItemIds(gang).Any(itemId =>
                    itemId < 0 || itemId >= definitions.Items.Count || definitions.Items[itemId].Type == 99)))
                throw new ArgumentException($"Player {player.Id} contains invalid equipped item state.", nameof(players));
            if (player.ResearchProgress.Any(pair =>
                    !IsActualItem(definitions, pair.Key) || pair.Value <= 0))
                throw new ArgumentException($"Player {player.Id} contains invalid research progress.", nameof(players));
            if (player.ResearchedItems.Any(itemId => !IsActualItem(definitions, itemId)))
                throw new ArgumentException($"Player {player.Id} contains an invalid researched item.", nameof(players));
            if (player.ResearchProgress.Keys.Any(player.ResearchedItems.Contains))
                throw new ArgumentException($"Player {player.Id} has overlapping active and completed research.", nameof(players));
            if (player.Inventory.Any(pair => !IsActualItem(definitions, pair.Key) || pair.Value <= 0))
                throw new ArgumentException($"Player {player.Id} contains invalid inventory state.", nameof(players));
        }

        var overcrowded = players.SelectMany(player => player.Gangs)
            .GroupBy(gang => (gang.Owner, gang.SectorId))
            .FirstOrDefault(group => group.Count() > MatchLimits.FriendlyGangsPerSector);
        if (overcrowded is not null)
            throw new ArgumentException($"Player {overcrowded.Key.Owner} exceeds sector {overcrowded.Key.SectorId} capacity.", nameof(players));

        if (sectors.SelectMany(sector => sector.Sites)
            .Any(site => !definitions.Sites.Any(definition => definition.Id == site.DefinitionId)))
            throw new ArgumentException("A sector contains an unknown site definition.", nameof(sectors));
        var playerIds = players.Select(player => player.Id).ToHashSet();
        if (sectors.Any(sector => sector.Owner is { } owner && !playerIds.Contains(owner)))
            throw new ArgumentException("A sector owner is not part of the match.", nameof(sectors));
        if (sectors.SelectMany(sector => sector.Sites)
            .Any(site => site.InfluencedBy is { } owner && !playerIds.Contains(owner)))
            throw new ArgumentException("A site influencer is not part of the match.", nameof(sectors));
    }

    private static IEnumerable<short> EquippedItemIds(MatchGangState gang)
    {
        if (gang.WeaponItemId is { } weapon) yield return weapon;
        if (gang.ArmorItemId is { } armor) yield return armor;
        if (gang.MiscellaneousItemId is { } miscellaneous) yield return miscellaneous;
    }

    private static bool IsActualItem(OriginalData definitions, short itemId) =>
        itemId >= 0 && itemId < definitions.Items.Count && definitions.Items[itemId].Type != 99;
}

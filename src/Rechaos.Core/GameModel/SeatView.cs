using Rechaos.Core.Assets;
using Rechaos.Core.Persistence;

namespace Rechaos.Core.GameModel;

/// <summary>
/// The match as one seat may know it at its planning entry: what the original draws for that
/// player then, and nothing else. docs/MULTIPLAYER.md, "What a seat may know", lists every part
/// of the state with the spec entries that decide it; this class is that table in code.
/// </summary>
/// <remarks>
/// <para>
/// A view is a <see cref="MatchState"/> so that the client plans on it with the same validator,
/// option catalog and screens it uses for a whole match. It is restored from the save document
/// of the whole match with every hidden part removed or set to a neutral value, so a reader that
/// looks for a hidden fact finds nothing rather than a guess. <see cref="MatchState.ViewedBy"/>
/// marks it, and every call that draws or resolves throws on it.
/// </para>
/// <para>
/// What a view leaves out: other seats' gangs the seat does not detect (RULE-DETECT-001) and
/// every gone gang of theirs; other seats' orders, cash, Support, research, hire offers, hire
/// orders, statistics, reports and Comlink inboxes; site progress and the sites' parts of
/// Support, Cash and base Tolerance in sectors the seat does not own (RULE-UI-011,
/// RULE-SEARCH-002, SCR-UI-004, SCR-UI-007); the random state, the seed and every die rolled;
/// the computer players' planning state and attitudes; the phase hashes; and every event the
/// seat's screens do not show. Other seats' scenario scores are replaced by the coarsest scores
/// that place every portrait of the Player Rankings panel where the true ones do
/// (SCR-OBJECTIVE-001), with the seat's own score exact.
/// </para>
/// </remarks>
public static class SeatView
{
    /// <summary>
    /// <paramref name="seat"/>'s view of <paramref name="state"/>, which must be in Command with
    /// the seat still in the match. The view is in Command with the seat as the active player, as
    /// at that player's planning entry.
    /// </summary>
    public static MatchState Project(MatchState state, PlayerId seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ViewedBy is not null)
            throw new ArgumentException("A view is projected from the whole match, not from another view.", nameof(state));
        var viewer = state.FindPlayer(seat) ?? throw new ArgumentOutOfRangeException(nameof(seat));
        if (state.Coordinator.Phase != TurnPhase.Command)
            throw new InvalidOperationException($"A seat's view is taken at its planning entry, not in {state.Coordinator.Phase}.");
        if (state.Outcome is not null)
            throw new InvalidOperationException("A finished match hides nothing; it is sent whole.");
        if (viewer.Status != PlayerStatus.Active)
            throw new InvalidOperationException("An eliminated seat plans nothing and has no view.");

        var whole = NativeSaveSerializer.Capture(state, fingerprint: false);
        var events = ViewEvents(state, seat, out var renumbered);
        var commands = OwnCommands(whole.Runtime.Commands, seat);
        var view = whole with
        {
            Setup = whole.Setup with { InitialSeed = 0 },
            Players = WithRankingScores(whole.Players.Select(player => player.Id == seat.Value
                ? OwnPlayer(player, seat)
                : OtherPlayer(state, player, seat)).ToArray(), state, seat),
            // Capture never writes a sector's Chaos, so only the crackdown history is cleared.
            Sectors = whole.Sectors.Select(sector => (sector.Owner == seat.Value
                    ? sector with { CrackdownHistory = [] }
                    : OtherSector(state.Definitions, sector)) with
                {
                    // SCR-UI-003 draws the police badge while crackdown_turns is above 0 and no
                    // screen draws the count, which a Crackdown raises by a die roll
                    // (RULE-POLICE-002). The sign keeps every test the rules make of it.
                    CrackdownTurnsRemaining = Math.Sign(sector.CrackdownTurnsRemaining ?? 0)
                }).ToArray(),
            Runtime = whole.Runtime with
            {
                Phase = TurnPhase.Command,
                ExecutionPhase = null,
                ActivePlayer = seat.Value,
                RandomState = 0,
                RandomConsumptionCount = 0,
                Commands = commands,
                NextCommandSequence = commands.Count,
                Events = events,
                NextEventSequence = events.Count,
                Notifications = whole.Runtime.Notifications.Select(entry => entry.Player == seat.Value
                    ? entry with
                    {
                        Items = entry.Items.Select(item => item with
                        {
                            RelatedEventSequence = item.RelatedEventSequence is { } related
                                ? renumbered[related]
                                : null
                        }).ToArray()
                    }
                    : new PlayerNotificationsDocument(entry.Player, 0, [])).ToArray(),
                PhaseHashes = [],
                Outcome = null,
                AiStrategy = new AiStrategyDocument(
                    new int[MatchLimits.PlayerCount],
                    new int[MatchLimits.PlayerCount * MatchLimits.PlayerCount]),
                AiPlanning = NativeSaveSerializer.CaptureAiPlanning(AiPlanningState.Initialize()),
                Comlink = whole.Runtime.Comlink!.Select(entry => entry.Player == seat.Value
                    ? entry
                    : new PlayerComlinkDocument(entry.Player, 0, -1, [], [])).ToArray(),
                // The payload names its seat, so every load of it, through this class or not,
                // restores a view that refuses to draw or resolve.
                ViewedBy = seat.Value
            }
        };
        return NativeSaveSerializer.RestoreDocument(view, state.Definitions, verifyStateFingerprint: false);
    }

    /// <summary>Writes <paramref name="view"/> as a save payload, the form it travels in.</summary>
    public static void Save(Stream destination, MatchState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.ViewedBy is null)
            throw new ArgumentException("Only a seat's view is written here; the whole match is saved with NativeSaveSerializer.", nameof(view));
        NativeSaveSerializer.Save(destination, view);
    }

    /// <summary>
    /// Reads a view <see cref="Save"/> wrote, refusing a payload that is not
    /// <paramref name="seat"/>'s view, including a whole match.
    /// </summary>
    public static MatchState Load(Stream source, OriginalData definitions, PlayerId seat)
    {
        var view = NativeSaveSerializer.Load(source, definitions);
        if (view.ViewedBy != seat)
            throw new InvalidDataException(view.ViewedBy is { } other
                ? $"The payload is seat {other.Value}'s view, not seat {seat.Value}'s."
                : "The payload is a whole match, not a seat's view.");
        return view;
    }

    // The seat's own record, less what it says about the other seats: which of them detect its
    // gangs (RULE-DETECT-001 writes visible_to for every observer, and no screen shows it).
    private static PlayerDocument OwnPlayer(PlayerDocument player, PlayerId seat) => player with
    {
        Gangs = player.Gangs.Select(gang => gang with { VisibilityMask = SeatBit(seat) }).ToArray()
    };

    private static PlayerDocument OtherPlayer(MatchState state, PlayerDocument player, PlayerId seat)
    {
        var live = state.Players.First(candidate => candidate.Id.Value == player.Id);
        var seen = new List<GangDocument>();
        for (var slot = 0; slot < player.Gangs.Count; slot++)
        {
            // RULE-DETECT-001, RULE-UI-010: the seat sees another seat's gang that is in play and
            // that its detection reached at the planning entry. A gone gang is in no screen.
            if (!live.Gangs[slot].IsActive || (live.Gangs[slot].VisibilityMask & SeatBit(seat)) == 0) continue;
            seen.Add(player.Gangs[slot] with
            {
                Hidden = false,
                HiredThisTurn = false,
                RetiredForce = null,
                VisibilityMask = (byte)(SeatBit(seat) | SeatBit(live.Id))
            });
        }
        return player with
        {
            Cash = 0,
            Support = 0,
            BigManPoints = 0,
            Gangs = seen,
            HirePool = [],
            PendingHires = [],
            ResearchProgress = new Dictionary<short, int>(),
            ResearchedItems = [],
            Inventory = new Dictionary<short, int>(),
            Statistics = new StatisticsDocument(0, 0, 0, 0, 0, 0),
            SnubbedHireOffer = null,
            HireOfferSlots = Enumerable.Repeat(HireOfferSlotState.Uninitialized, MatchLimits.HireOffersPerPlayer).ToArray(),
            SnubbedHireOfferSlot = null
        };
    }

    // RULE-UI-011, SCR-UI-004, SCR-UI-007, RULE-SEARCH-002: in a sector the seat does not own, a
    // site shows its definition's Resistance, no progress meter and no controlled marker, and the
    // console shows 0 for Support and Cash. Income, Tolerance, the owner, the police and the
    // site types are shown to everybody and stay.
    private static SectorDocument OtherSector(OriginalData definitions, SectorDocument sector) => sector with
    {
        Sites = sector.Sites.Select(site => site with
        {
            Resistance = definitions.Site(site.DefinitionId).Resistance,
            InfluencedBy = null
        }).ToArray(),
        CrackdownHistory = [],
        // With no site counted, the base is the Tolerance the console shows.
        BaseTolerance = sector.Tolerance,
        Support = 0,
        CashYield = 1
    };

    // The seat's own orders, numbered from 0 in the order they were given: the queue's numbers
    // count every seat's orders, so they would tell the seat how many the others gave.
    private static IReadOnlyList<QueuedCommand> OwnCommands(IReadOnlyList<QueuedCommand> commands, PlayerId seat) =>
        commands.Where(command => command.Command.Player == seat)
            .OrderBy(command => command.Sequence)
            .Select((command, index) => command with { Sequence = index })
            .ToArray();

    /// <summary>
    /// The events the seat's screens read, numbered from 0 with their dice removed, and the map
    /// from each kept event's sequence to its new one.
    /// </summary>
    /// <remarks>
    /// Kept: every elimination and the end of the match (RULE-EVENT-003); the seat's own events
    /// of the last resolution and of this turn; every fight and every police attack that found a
    /// gang in the last resolution, in each sector where the seat fought or has a gang now, which
    /// are the Combat Results pages with every player's row on them (FND-COMBAT-012) and include
    /// the fights Detailed Combat plays (RULE-COMBAT-004); and every event one of the seat's
    /// reports names. No screen shows a die, and the rolls of a resolution are draws of the random
    /// state the view hides.
    /// </remarks>
    private static IReadOnlyList<GameEvent> ViewEvents(
        MatchState state, PlayerId seat, out IReadOnlyDictionary<long, long> renumbered)
    {
        var currentTurn = state.Coordinator.Turn;
        var lastTurn = currentTurn - 1;
        var pages = state.FindPlayer(seat)!.Gangs
            .Where(gang => gang.IsActive)
            .Select(gang => gang.SectorId)
            .ToHashSet();
        // A police roll that missed the seat's gang opens no page: the gang may have left the
        // sector in Movement, and the page would show the other seats' fights there.
        foreach (var gameEvent in state.Events)
            if (gameEvent.Turn == lastTurn && Fight(state, gameEvent) is { Shown: true } fight
                && (fight.First == seat || fight.Second == seat))
                pages.Add(fight.Sector);
        var reported = state.NotificationsFor(seat)
            .Select(notification => notification.RelatedEventSequence)
            .OfType<long>()
            .ToHashSet();
        var kept = new List<GameEvent>();
        var map = new Dictionary<long, long>();
        foreach (var gameEvent in state.Events)
        {
            if (!reported.Contains(gameEvent.Sequence)
                && !SeatMayKnow(state, gameEvent, seat, lastTurn, currentTurn, pages))
                continue;
            map[gameEvent.Sequence] = kept.Count;
            kept.Add(WithoutDice(gameEvent) with { Sequence = kept.Count });
        }
        renumbered = map;
        return kept;
    }

    private static bool SeatMayKnow(
        MatchState state, GameEvent gameEvent, PlayerId seat, int lastTurn, int currentTurn,
        IReadOnlySet<int> pages)
    {
        if (gameEvent.Kind is GameEventKind.PlayerEliminated or GameEventKind.MatchEnded) return true;
        if (gameEvent.Turn == currentTurn) return gameEvent.Player == seat;
        if (gameEvent.Turn != lastTurn) return false;
        if (gameEvent.Player == seat) return true;
        return Fight(state, gameEvent) is { } fight && fight.Shown && pages.Contains(fight.Sector);
    }

    /// <summary>
    /// The sector and the two players of a fight, or of a police attack with no second player;
    /// null for any other event. A police attack that found no gang is never shown.
    /// </summary>
    private static (int Sector, PlayerId First, PlayerId? Second, bool Shown)? Fight(MatchState state, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            return gameEvent.PoliceAttack is { } police
                ? (police.SectorId, gameEvent.Player, null, police.Detected && gameEvent.Gang is { } target
                    && (state.FindGang(target) is not null || police.Target is not null))
                : null;
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution is not { } resolution
            || gameEvent.Target.Kind != CommandTargetKind.Gang || gameEvent.Gang is not { } attacker)
            return null;
        // As the Combat Results screen finds its combatants: the live gang while the roster holds
        // it, else the one the event recorded, and no page for a fight that lacks either.
        var attackerSector = state.FindGang(attacker)?.SectorId ?? resolution.Attacker?.SectorId;
        var defender = state.FindGang(new GangId(gameEvent.Target.Id))?.Owner ?? resolution.Defender?.Owner;
        return attackerSector is { } sector && defender is { } second
            ? (sector, gameEvent.Player, second, true)
            : null;
    }

    private static GameEvent WithoutDice(GameEvent gameEvent) => gameEvent with
    {
        Resolution = gameEvent.Resolution is { } resolution
            ? resolution with
            {
                Rolls = [],
                RetaliationRolls = resolution.RetaliationRolls is null ? null : [],
                DetectionRoll = null,
                ChanceRoll = null
            }
            : null,
        PoliceAttack = gameEvent.PoliceAttack is { } police
            ? police with { Rolls = [], DetectionRoll = 0 }
            : null
    };

    private static byte SeatBit(PlayerId seat) => (byte)(1 << seat.Value);

    // RULE-OBJECTIVE-002, SCR-OBJECTIVE-001: the seat sees its own score on the console and every
    // active seat's portrait on its rail. Other seats keep a score that puts their portrait where
    // the true one does, and no more.
    private static IReadOnlyList<PlayerDocument> WithRankingScores(
        IReadOnlyList<PlayerDocument> players, MatchState state, PlayerId seat)
    {
        var scores = RankingScores(
            state.Players.Select(player => (player.Id, (long)player.ScenarioScore,
                player.Status == PlayerStatus.Active)).ToArray(),
            seat);
        return players.Select(player => player with
        {
            ScenarioScore = checked((int)scores[new PlayerId(player.Id)])
        }).ToArray();
    }

    /// <summary>
    /// Scores for every player that place each active portrait of the Player Rankings panel where
    /// the true scores place it (<see cref="ScenarioScoreRail.Offset"/>), with
    /// <paramref name="seat"/>'s own score exact and the spread between the highest and the
    /// lowest as small as that allows. An inactive player keeps the score it has, which is
    /// -32000 for every player out of the match (RULE-OBJECTIVE-002).
    /// </summary>
    internal static IReadOnlyDictionary<PlayerId, long> RankingScores(
        IReadOnlyList<(PlayerId Player, long Score, bool Active)> players, PlayerId seat)
    {
        ArgumentNullException.ThrowIfNull(players);
        var result = players.ToDictionary(player => player.Player, player => player.Score);
        var active = players.Where(player => player.Active).ToArray();
        var own = active.Single(player => player.Player == seat);
        var high = active.Max(player => player.Score);
        var low = active.Min(player => player.Score);
        if (high == low)
        {
            foreach (var player in active) result[player.Player] = own.Score;
            return result;
        }
        var offsets = active.ToDictionary(
            player => player.Player, player => ScenarioScoreRail.Offset(player.Score, high, low));
        var deepest = offsets.Values.Max();
        var trueRange = high - low + 1;
        for (var range = 2L; range <= trueRange; range++)
        {
            // The lowest score is the bottom of the range, so a portrait at the deepest offset
            // has to sit exactly range - 1 below the top.
            if (RailOffset(range - 1, range) != deepest) continue;
            var depths = new Dictionary<PlayerId, long>();
            foreach (var (player, offset) in offsets)
            {
                if (offset == deepest) { depths[player] = range - 1; continue; }
                if (SmallestDepth(offset, range) is not { } depth) break;
                depths[player] = depth;
            }
            if (depths.Count != offsets.Count) continue;
            var top = own.Score + depths[seat];
            foreach (var (player, depth) in depths) result[player] = top - depth;
            return result;
        }
        // The true scores always place every portrait, so the loop returns by trueRange.
        throw new InvalidOperationException("No scores reproduce the ranking rails.");
    }

    // How far below the top a score sits to be drawn at offset, given a range; null when no score
    // in the range is drawn there.
    private static long? SmallestDepth(int offset, long range)
    {
        var estimate = (long)Math.Floor((double)offset * range / ScenarioScoreRail.Length);
        for (var depth = Math.Max(0, estimate - 2); depth <= Math.Min(range - 1, estimate + 3); depth++)
            if (RailOffset(depth, range) == offset) return depth;
        return null;
    }

    private static int RailOffset(long depth, long range) => ScenarioScoreRail.Offset(-depth, 0, 1 - range);
}

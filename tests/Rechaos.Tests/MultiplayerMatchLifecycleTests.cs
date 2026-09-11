using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// What an online match looks like at its edges: the turn that ends it, a seat whose player has gone,
/// and a sealed set that is not a shape any client can apply.
/// </summary>
/// <remarks>
/// The middle of a match is covered by <see cref="MultiplayerSealedTurnTests"/>, which is where two
/// clients are held to the same hash. These are the cases that only happen once, and so the ones that
/// reached a player before they reached a test.
/// </remarks>
public sealed class MultiplayerMatchLifecycleTests
{
    private const int Seed = 1996;

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", WirePlayerStatus.Active, IsHost: false),
    ];

    private static (MatchReplayRecorder Replay, OriginalData Definitions) NewClient(
        IReadOnlyList<PlayerView>? roster = null)
    {
        var definitions = BundledOriginalData.Load();
        var state = MatchBootstrapFactory.Create(definitions, Seed, Settings, roster ?? Roster);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);
        return (replay, definitions);
    }

    private static SealedOrdersView Sealed(int turn, params (int Slot, OrderDocument Orders)[] entries)
    {
        var players = entries
            .Select(entry => new SealedPlayerOrders(
                $"p{entry.Slot + 1}", entry.Slot, entry.Orders, OrderDigest.OfDocument(entry.Orders)))
            .ToArray();
        return new SealedOrdersView(turn, OrderDigest.OfSet(players), players);
    }

    private static OrderDocument Empty => new(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);

    /// <summary>
    /// The turn that ends a match leaves it short of a Command phase, with an outcome.
    /// </summary>
    /// <remarks>
    /// The fact the whole end-of-match path rests on, and the one the interface used to get wrong. The
    /// phase loop stops as soon as there is an outcome, so the state a client is left holding after the
    /// final seal is in Upkeep — not Command, and so not a state a turn can be planned on. Anything
    /// that reaches for one without asking about the outcome first fails here, at the end of every
    /// match, which is the worst possible time to find out.
    /// </remarks>
    [Fact]
    public void TheTurnThatEndsAMatchStopsShortOfACommandPhase()
    {
        var (replay, _) = NewClient();

        var turns = PlayToTheEnd(replay);

        Assert.NotNull(replay.State.Outcome);
        Assert.Equal(MatchEndReason.TimeLimit, replay.State.Outcome!.Reason);
        Assert.NotEqual(TurnPhase.Command, replay.State.Coordinator.Phase);
        Assert.Equal(ScenarioCatalog.Turns(GameDuration.SixMonths), turns);
    }

    /// <summary>
    /// Planning a turn on a finished match is refused, rather than half-done.
    /// </summary>
    /// <remarks>
    /// Stated as a test because the interface has to branch on the outcome before it ever gets here,
    /// and a precondition nobody can see is a precondition somebody removes. The refusal is what makes
    /// the mistake loud if that branch is ever lost again.
    /// </remarks>
    [Fact]
    public void RefusesToPlanATurnOnAFinishedMatch()
    {
        var (replay, definitions) = NewClient();
        PlayToTheEnd(replay);

        Assert.Throws<InvalidOperationException>(
            () => SpeculativeTurn.For(replay.State, definitions, slot: 0));
    }

    /// <summary>
    /// A repeat of the final seal is dropped, not applied to a match that cannot take one.
    /// </summary>
    /// <remarks>
    /// Delivery is at least once, and the turn that ends a match is as repeatable as any other. The
    /// coordinator advances even though the phase stops short of Command, which is what keeps the
    /// "already applied" test — the turn number — working on the one turn where the phase test would
    /// not.
    /// </remarks>
    [Fact]
    public void AdvancesTheTurnCounterEvenOnTheSealThatEndsTheMatch()
    {
        var (replay, _) = NewClient();

        var turns = PlayToTheEnd(replay);

        Assert.True(
            replay.State.Coordinator.Turn > turns,
            "a repeated seal is dropped by comparing its turn against the coordinator's, so the "
            + "counter has to move even when the phase does not reach Command");
    }

    /// <summary>
    /// A player who has left is seated exactly as they were when the match started.
    /// </summary>
    /// <remarks>
    /// The city is generated from the roster, and the rules read player names — starting cash is
    /// granted by one. So a bootstrap that read a player's current status would build a different city
    /// depending on when it ran: a client joining the match before a departure would seat a human
    /// under their own name, and one bootstrapping after would seat a computer player under a derived
    /// one. Both would be certain they were right, and they would disagree from the first upkeep.
    /// </remarks>
    [Fact]
    public void SeatsADepartedPlayerTheSameWayItSeatedThemAtTheStart()
    {
        IReadOnlyList<PlayerView> afterLeaving =
        [
            Roster[0],
            new("p2", 1, "GRACE", WirePlayerStatus.Left, IsHost: false),
        ];

        var atStart = MatchBootstrapFactory.Setup(Seed, Settings, Roster);
        var later = MatchBootstrapFactory.Setup(Seed, Settings, afterLeaving);

        Assert.Equal(atStart.Players[1].Name, later.Players[1].Name);
        Assert.Equal(atStart.Players[1].Controller, later.Players[1].Controller);
        Assert.Equal(PlayerController.Human, later.Players[1].Controller);
    }

    /// <summary>
    /// A seat whose player has gone is left alone, not handed to the computer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which seats the computer plays is read out of the hashed state, so every client agrees about it
    /// without being told. A human seat with no document in the sealed set therefore does nothing —
    /// which is what a player who ran out of clock ordered, and what a player who left keeps ordering.
    /// </para>
    /// <para>
    /// The assertion is that this succeeds at all: the core refuses to plan an AI turn for a seat a
    /// human controls, so a client that decided from a roster it had fetched separately — and so could
    /// hold a newer version of than its peers — would throw here rather than quietly diverge.
    /// </para>
    /// </remarks>
    [Fact]
    public void LeavesASeatedPlayersSlotIdleWhenTheSealCarriesNoDocumentForIt()
    {
        var (replay, _) = NewClient();

        var hash = SealedTurnApplier.Apply(replay, Sealed(1, (0, Empty)));

        Assert.Equal(MatchStateHasher.ComputeSha256(replay.State), hash);
        Assert.DoesNotContain(
            replay.Steps,
            step => step.Kind == ReplayOperationKind.PrepareAiPlanning
                && step.Player == new PlayerId(1));
    }

    /// <summary>Every seat nobody took is planned by the AI, on every client, from the shared state.</summary>
    [Fact]
    public void PlansTheSeatsNobodyTook()
    {
        var (replay, _) = NewClient();

        SealedTurnApplier.Apply(replay, Sealed(1, (0, Empty), (1, Empty)));

        var planned = replay.Steps
            .Where(step => step.Kind == ReplayOperationKind.PrepareAiPlanning)
            .Select(step => step.Player!.Value.Value)
            .Order()
            .ToArray();
        Assert.Equal([2, 3, 4, 5], planned);
    }

    /// <summary>
    /// A name the original rules read as a cheat code is replaced with the seat's own.
    /// </summary>
    /// <remarks>
    /// The lobby refuses these, so this is the safety net for a server that did not. Substituting the
    /// derived seat name is deterministic, which is the property that matters: every client substitutes
    /// the same one, so the match stays in step while nobody gets the bonus.
    /// </remarks>
    [Fact]
    public void NeutralisesACheatNameIntoTheSeatsOwnName()
    {
        IReadOnlyList<PlayerView> cheating =
        [
            new("p1", 0, "SMGFUNDAGE", WirePlayerStatus.Active, IsHost: true),
            Roster[1],
        ];

        var setup = MatchBootstrapFactory.Setup(Seed, Settings, cheating);

        Assert.Equal("PLAYER 1", setup.Players[0].Name);
        Assert.Equal(PlayerController.Human, setup.Players[0].Controller);
    }

    /// <summary>
    /// A cheat name taken online buys nothing, all the way through to the cash on the table.
    /// </summary>
    /// <remarks>
    /// The point of the substitution, asserted against the rule it exists to defeat rather than
    /// against the name it rewrites: two matches that differ only in that one player tried it have to
    /// be the same match.
    /// </remarks>
    [Fact]
    public void ACheatNameChangesNothingAboutTheMatchItIsUsedIn()
    {
        IReadOnlyList<PlayerView> cheating =
        [
            new("p1", 0, "SMGFUNDAGE", WirePlayerStatus.Active, IsHost: true),
            Roster[1],
        ];
        IReadOnlyList<PlayerView> honest =
        [
            new("p1", 0, "PLAYER 1", WirePlayerStatus.Active, IsHost: true),
            Roster[1],
        ];

        var (cheated, _) = NewClient(cheating);
        var (played, _) = NewClient(honest);

        Assert.Equal(
            MatchStateHasher.ComputeSha256(played.State),
            MatchStateHasher.ComputeSha256(cheated.State));
    }

    /// <summary>
    /// A sealed set naming one slot twice is refused rather than resolved by enumeration order.
    /// </summary>
    /// <remarks>
    /// Which of the two documents the turn contained would otherwise come down to how a dictionary was
    /// built, which is not something two clients can be relied on to agree about — and a disagreement
    /// here is a desync rather than an error anybody can see.
    /// </remarks>
    [Fact]
    public void RefusesASealedSetNamingASlotTwice()
    {
        var (replay, _) = NewClient();
        var doubled = Sealed(1, (0, Empty), (0, Empty));

        Assert.Throws<MultiplayerProtocolException>(() => SealedTurnApplier.Apply(replay, doubled));
    }

    /// <summary>A sealed set naming a slot this match does not have is refused by name.</summary>
    [Fact]
    public void RefusesASealedSetNamingASlotTheMatchDoesNotHave()
    {
        var (replay, _) = NewClient();
        var offTable = Sealed(1, (MatchLimits.PlayerCount, Empty));

        var failure = Assert.Throws<MultiplayerProtocolException>(
            () => SealedTurnApplier.Apply(replay, offTable));
        Assert.Contains($"slot {MatchLimits.PlayerCount}", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applies empty sealed turns until the match ends, answering the turn it ended on.
    /// </summary>
    /// <remarks>
    /// Six months is twenty-six turns, which is long enough to be worth doing once per test and short
    /// enough to do at all. Nobody orders anything: the scenario is timed, so the clock ends it
    /// whatever the players did.
    /// </remarks>
    private static int PlayToTheEnd(MatchReplayRecorder replay)
    {
        var limit = ScenarioCatalog.Turns(GameDuration.SixMonths) + 2;
        for (var turn = 1; turn <= limit; turn++)
        {
            SealedTurnApplier.Apply(replay, Sealed(turn, (0, Empty), (1, Empty)));
            if (replay.State.Outcome is not null) return turn;
        }
        throw new InvalidOperationException($"the match had not ended after {limit} turns");
    }
}

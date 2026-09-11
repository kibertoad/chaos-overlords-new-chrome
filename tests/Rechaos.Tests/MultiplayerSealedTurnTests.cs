using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using CoreTarget = Rechaos.Core.GameModel.CommandTarget;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The lockstep contract, exercised the way a match exercises it: two independent clients bootstrap
/// from the same seed and roster, apply the same sealed set, and have to agree on the state hash.
/// </summary>
/// <remarks>
/// This is what the whole protocol reduces to. The server holds no rules, so nothing but agreement
/// here keeps a match consistent, and every hash reported to it is produced by this path.
/// </remarks>
public sealed class MultiplayerSealedTurnTests
{
    private const int Seed = 1996;

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", WirePlayerStatus.Active, IsHost: false),
    ];

    private static (MatchReplayRecorder Replay, OriginalData Definitions) NewClient()
    {
        var definitions = BundledOriginalData.Load();
        var state = MatchBootstrapFactory.Create(definitions, Seed, Settings, Roster);
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

    /// <summary>
    /// The city is generated, not downloaded: the seed, the settings and the seating are the whole
    /// input, and two clients given them arrive at the same state before a single order is sent.
    /// </summary>
    [Fact]
    public void TwoClientsBootstrapTheSameCityFromTheSeedAndRoster()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();

        Assert.Equal(
            MatchStateHasher.ComputeSha256(left.State),
            MatchStateHasher.ComputeSha256(right.State));
        Assert.Equal(TurnPhase.Command, left.State.Coordinator.Phase);
        Assert.Equal(MatchLimits.PlayerCount, left.State.Setup.Players.Count);
    }

    /// <summary>
    /// Only the seated slots are human. Every other seat is a computer player planned identically on
    /// every client, which is why its orders never cross the wire.
    /// </summary>
    [Fact]
    public void SeatsTheRosterAndLeavesEveryOtherSlotToTheComputer()
    {
        var (replay, _) = NewClient();
        var controllers = replay.State.Setup.Players.Select(player => player.Controller).ToArray();

        Assert.Equal(PlayerController.Human, controllers[0]);
        Assert.Equal(PlayerController.Human, controllers[1]);
        Assert.All(controllers.Skip(2), controller => Assert.Equal(PlayerController.Computer, controller));
        Assert.Equal("ADA", replay.State.Setup.Players[0].Name);
    }

    /// <summary>
    /// The heart of it: the same sealed set applied to two independent clients leaves both on the
    /// same state, and so on the same hash they report to the server.
    /// </summary>
    [Fact]
    public void ApplyingTheSameSealedSetLeavesEveryClientOnTheSameHash()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();
        var orders = Sealed(1, (0, OrdersFor(left.State, 0)), (1, OrdersFor(left.State, 1)));

        var leftHash = SealedTurnApplier.Apply(left, orders);
        var rightHash = SealedTurnApplier.Apply(right, orders);

        Assert.Equal(leftHash, rightHash);
        Assert.Equal(MatchStateHasher.ComputeSha256(left.State), leftHash);
        Assert.Equal(2, left.State.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, left.State.Coordinator.Phase);
    }

    /// <summary>
    /// A turn where nobody ordered anything still resolves: every seat is planned, the automatic
    /// phases run, and the match arrives at the next Command phase.
    /// </summary>
    [Fact]
    public void ResolvesATurnNobodyOrderedAnythingIn()
    {
        var (replay, _) = NewClient();

        var hash = SealedTurnApplier.Apply(replay, Sealed(1));

        Assert.Equal(MatchStateHasher.ComputeSha256(replay.State), hash);
        Assert.Equal(2, replay.State.Coordinator.Turn);
    }

    /// <summary>
    /// Several turns in a row stay in step, which is the property a single turn cannot show: a
    /// difference in how the PRNG was spent surfaces on the turn after the one that spent it.
    /// </summary>
    [Fact]
    public void StaysInStepOverSeveralTurns()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();

        for (var turn = 1; turn <= 4; turn++)
        {
            var orders = Sealed(turn, (0, OrdersFor(left.State, 0)));
            Assert.Equal(
                SealedTurnApplier.Apply(left, orders),
                SealedTurnApplier.Apply(right, orders));
        }
        Assert.Equal(5, left.State.Coordinator.Turn);
    }

    /// <summary>
    /// Ops are applied under the slot the seal attributed them to, never under the `player` field
    /// inside them, so a document that lies about its author cannot act for anybody else.
    /// </summary>
    [Fact]
    public void AttributesOpsToTheSlotTheSealFrozeThemUnder()
    {
        var (honest, _) = NewClient();
        var (lying, _) = NewClient();
        var gang = honest.State.Players[1].Gangs[0].Id.Value;

        // Slot 0's document, but every op claims to be slot 1's.
        var forged = new OrderDocument(1, [new CancelCommandOp(1, gang)]);
        var truthful = new OrderDocument(1, [new CancelCommandOp(0, gang)]);

        Assert.Equal(
            SealedTurnApplier.Apply(honest, Sealed(1, (0, truthful))),
            SealedTurnApplier.Apply(lying, Sealed(1, (0, forged))));
    }

    /// <summary>
    /// A document recorded through the builder is the document that comes back off the wire: the
    /// same ops, the same targets, the same digest.
    /// </summary>
    [Fact]
    public void RoundTripsAnOrderDocumentThroughTheWire()
    {
        var builder = new OrderDocumentBuilder(new PlayerId(0));
        builder.Submit(new GameCommand(
            new PlayerId(0), new GangId(3), GangAction.Move, CoreTarget.Sector(27),
            Repeat: true, SecondaryTarget: CoreTarget.Site(81)));
        builder.Cancel(new PlayerId(0), new GangId(4));
        builder.QueueHire(new PlayerId(0), 44, 27);
        builder.SnubHireOffer(new PlayerId(0), 12);
        builder.DismissNotification(new PlayerId(0));
        var document = builder.Build();

        var wire = WireJson.Read<OrderDocument>(WireJson.Write(document));

        Assert.Equal(OrderDigest.OfDocument(document), OrderDigest.OfDocument(wire));
        var submit = Assert.IsType<SubmitCommandOp>(wire.Ops[0]);
        Assert.Equal(27, Assert.IsType<SectorTarget>(submit.Target).Id);
        Assert.Equal(81, Assert.IsType<SiteTarget>(submit.SecondaryTarget).Id);
        Assert.True(submit.Repeat);
        Assert.Equal(5, wire.Ops.Count);
    }

    /// <summary>An op for another slot is a client bug, and is refused where it is built.</summary>
    [Fact]
    public void RefusesToRecordAnOpForAnotherSlot()
    {
        var builder = new OrderDocumentBuilder(new PlayerId(1));

        Assert.Throws<InvalidOperationException>(
            () => builder.Cancel(new PlayerId(0), new GangId(1)));
    }

    /// <summary>
    /// The interface plans on a copy, and the copy makes the local seat the active one — without
    /// which the core refuses everything a player in any seat but the first tries.
    /// </summary>
    [Fact]
    public void PlansOnACopyThatMakesTheLocalSeatActive()
    {
        var (authoritative, definitions) = NewClient();
        var before = MatchStateHasher.ComputeSha256(authoritative.State);

        var turn = SpeculativeTurn.For(authoritative.State, definitions, slot: 1);
        var gang = turn.State.FindPlayer(new PlayerId(1))!.Gangs.First(g => g.IsActive).Id;
        var result = turn.Submit(new GameCommand(new PlayerId(1), gang, GangAction.Hide, CoreTarget.None));

        Assert.True(result.Accepted);
        Assert.Equal(new PlayerId(1), turn.State.Coordinator.ActivePlayer);
        // Recorded once, and the authoritative state is untouched by any of it.
        Assert.Equal(1, turn.Orders.Count);
        Assert.Equal(before, MatchStateHasher.ComputeSha256(authoritative.State));
    }

    /// <summary>
    /// The dock a player plans against is the dock the sealed turn grants: offers are drawn for
    /// every seat before anyone plans, so the copy inherits them rather than drawing its own.
    /// </summary>
    [Fact]
    public void ShowsTheSameHireOffersTheSealedTurnWillGrant()
    {
        var (authoritative, definitions) = NewClient();
        var authoritativeOffers = authoritative.State.FindPlayer(new PlayerId(1))!.HirePool;

        var turn = SpeculativeTurn.For(authoritative.State, definitions, slot: 1);

        Assert.NotEmpty(authoritativeOffers);
        Assert.Equal(authoritativeOffers, turn.State.FindPlayer(new PlayerId(1))!.HirePool);
    }

    /// <summary>An op the core refused is not recorded: it would spend the document's budget saying nothing.</summary>
    [Fact]
    public void RecordsOnlyWhatTheCoreAccepted()
    {
        var (authoritative, definitions) = NewClient();
        var turn = SpeculativeTurn.For(authoritative.State, definitions, slot: 0);

        // Slot 0 does not own this gang, so the core refuses the command.
        var foreignGang = turn.State.FindPlayer(new PlayerId(1))!.Gangs[0].Id;
        var result = turn.Submit(new GameCommand(new PlayerId(0), foreignGang, GangAction.Hide, CoreTarget.None));

        Assert.False(result.Accepted);
        Assert.Equal(0, turn.Orders.Count);
    }

    /// <summary>One turn's worth of a player's real commands, so the fixture is a legal document.</summary>
    private static OrderDocument OrdersFor(MatchState state, int slot)
    {
        var player = state.FindPlayer(new PlayerId(slot))!;
        var builder = new OrderDocumentBuilder(new PlayerId(slot));
        foreach (var gang in player.Gangs.Where(gang => gang.IsActive).Take(1))
        {
            builder.Submit(new GameCommand(
                new PlayerId(slot), gang.Id, GangAction.Hide, CoreTarget.None));
        }
        return builder.Build();
    }
}

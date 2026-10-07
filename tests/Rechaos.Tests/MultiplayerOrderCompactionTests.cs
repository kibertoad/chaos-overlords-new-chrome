using System.Text;
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
/// DEV-NET-002: an online turn's order document leaves out what a later order makes moot, and
/// never grows past the ops the server accepts.
/// </summary>
/// <remarks>
/// The document a player's clicks produce has to turn into the same match whether it is sent as
/// the whole log or compacted. These tests hold the two side by side: on the planning copy a
/// compacted draft restores to, and through a sealed turn that resolves.
/// </remarks>
public sealed class MultiplayerOrderCompactionTests
{
    private const int Seed = 1996;
    private static readonly PlayerId Local = new(0);

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    [Fact]
    public void AGangsLaterOrderReplacesItsEarlierOne()
    {
        var (authoritative, definitions) = NewClient();
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value);
        var gang = ActiveGangs(turn.State)[0];

        Assert.True(turn.Submit(Hide(gang)).Accepted);
        Assert.True(turn.Submit(Hide(gang, repeat: true)).Accepted);
        Assert.True(turn.Submit(Hide(gang)).Accepted);

        var op = Assert.IsType<SubmitCommandOp>(Assert.Single(turn.Build().Ops));
        Assert.Equal(gang.Value, op.Gang);
        Assert.False(op.Repeat);
    }

    [Fact]
    public void CancellingAnOrderGivenThisTurnLeavesNothingToSend()
    {
        var (authoritative, definitions) = NewClient();
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value);
        var gang = ActiveGangs(turn.State)[0];
        Assert.True(turn.Submit(Hide(gang)).Accepted);
        var version = turn.Orders.Version;

        Assert.True(turn.Cancel(gang).Accepted);

        Assert.Empty(turn.Build().Ops);
        Assert.NotEqual(version, turn.Orders.Version);
    }

    /// <summary>
    /// A recurring order carried into the turn is still queued on every client, so withdrawing it
    /// has to travel, whatever the player did with the gang in between.
    /// </summary>
    [Fact]
    public void CancellingAnOrderCarriedIntoTheTurnIsKept()
    {
        var (authoritative, definitions) = NewClient();
        var gang = ActiveGangs(authoritative.State)[0];
        Assert.True(authoritative.Submit(Hide(gang, repeat: true)).Accepted);
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value);

        Assert.True(turn.Cancel(gang).Accepted);
        Assert.IsType<CancelCommandOp>(Assert.Single(turn.Build().Ops));
        Assert.True(turn.Submit(Hide(gang)).Accepted);
        Assert.IsType<SubmitCommandOp>(Assert.Single(turn.Build().Ops));
        Assert.True(turn.Cancel(gang).Accepted);
        Assert.IsType<CancelCommandOp>(Assert.Single(turn.Build().Ops));
    }

    /// <summary>
    /// A hire or a snub clears whatever the dock held, and a second snub of the same offer
    /// withdraws it, so the dock is never more than one op.
    /// </summary>
    [Fact]
    public void TheHireDockIsAtMostOneOp()
    {
        var (authoritative, definitions) = NewClient();
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value);
        var (offers, sector) = HireChoices(turn.State);

        Assert.True(turn.QueueHire(offers[0], sector).Accepted);
        Assert.True(turn.QueueHire(offers[1], sector).Accepted);
        Assert.Equal(offers[1], Assert.IsType<QueueHireOp>(Assert.Single(turn.Build().Ops)).GangDefinitionId);

        Assert.True(turn.SnubHireOffer(offers[0]).Accepted);
        Assert.Equal(offers[0], Assert.IsType<SnubHireOfferOp>(Assert.Single(turn.Build().Ops)).GangDefinitionId);

        Assert.True(turn.SnubHireOffer(offers[0]).Accepted);
        Assert.Empty(turn.Build().Ops);
        Assert.Empty(turn.State.FindPlayer(Local)!.PendingHires);
        Assert.Null(turn.State.FindPlayer(Local)!.SnubbedHireOffer);

        Assert.True(turn.QueueHire(offers[1], sector).Accepted);
        Assert.True(turn.SnubHireOffer(offers[1]).Accepted);
        Assert.Empty(turn.Build().Ops);
    }

    /// <summary>
    /// The compacted document is the same turn as the log of every click: restored, it rebuilds
    /// the planning copy the player was looking at, and sealed, it resolves to the same city as
    /// the log does.
    /// </summary>
    [Fact]
    public void TheCompactedDocumentPlaysTheSameTurnAsTheWholeLog()
    {
        var (authoritative, definitions) = NewClient();
        var carried = ActiveGangs(authoritative.State)[^1];
        Assert.True(authoritative.Submit(Hide(carried, repeat: true)).Accepted);
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value);
        var log = new OrderDocumentBuilder(Local);
        PlayABusyTurn(turn, log, carried);

        var compacted = turn.Build();
        var whole = log.Build();
        Assert.True(compacted.Ops.Count < whole.Ops.Count,
            $"the busy turn compacted from {whole.Ops.Count} ops to {compacted.Ops.Count}");

        var restored = SpeculativeTurn.Restore(authoritative.State, definitions, Local.Value, compacted);
        Assert.Equal(Planning(turn.State), Planning(restored.State));

        var (fromLog, _) = NewClient();
        var (fromCompacted, _) = NewClient();
        Assert.True(fromLog.Submit(Hide(carried, repeat: true)).Accepted);
        Assert.True(fromCompacted.Submit(Hide(carried, repeat: true)).Accepted);
        SealedTurnApplier.Apply(fromLog, Sealed(fromLog.State, whole));
        SealedTurnApplier.Apply(fromCompacted, Sealed(fromCompacted.State, compacted));

        Assert.Equal(Resolved(fromLog.State), Resolved(fromCompacted.State));
    }

    /// <summary>
    /// The argument DEV-NET-002 rests on: a compacted turn holds at most one op per gang, one for
    /// the dock and one per notification, which leaves the server's limit out of reach of every
    /// input the original has.
    /// </summary>
    [Fact]
    public void TheOriginalsInputsCannotFillADocument()
    {
        Assert.Equal(512, OrderDocumentBuilder.MaxOps);
        Assert.True(MatchLimits.GangsPerPlayer + 1 + MatchLimits.NotificationsPerPlayer < OrderDocumentBuilder.MaxOps);
    }

    /// <summary>
    /// When the document is full, an action that would add an op is refused before the copy
    /// changes, so what the player sees is what the document says; one that replaces an op the
    /// document holds still goes through.
    /// </summary>
    [Fact]
    public void AFullDocumentRefusesWhatWouldGrowItAndNothingElse()
    {
        var (authoritative, definitions) = NewClient();
        var turn = SpeculativeTurn.For(authoritative.State, definitions, Local.Value, orderLimit: 1);
        var gangs = ActiveGangs(turn.State);
        var (offers, sector) = HireChoices(turn.State);
        Assert.True(turn.Submit(Hide(gangs[0])).Accepted);
        var version = turn.Orders.Version;
        var before = MatchStateHasher.ComputeFingerprint(turn.State);

        var command = turn.Submit(Hide(gangs[1]));
        var hire = turn.QueueHire(offers[0], sector);
        var snub = turn.SnubHireOffer(offers[0]);
        var dismissed = turn.DismissNotification();

        Assert.Equal(CommandValidationCode.OrderLimitReached, command.Validation.Code);
        Assert.Equal("Too many orders this turn.", command.Validation.Message);
        Assert.Equal(HireValidationCode.OrderLimitReached, hire.Validation.Code);
        Assert.Equal(HireValidationCode.OrderLimitReached, snub.Validation.Code);
        Assert.False(dismissed);
        Assert.Equal(before, MatchStateHasher.ComputeFingerprint(turn.State));
        Assert.Equal(version, turn.Orders.Version);

        Assert.True(turn.Submit(Hide(gangs[0], repeat: true)).Accepted);
        Assert.True(Assert.IsType<SubmitCommandOp>(Assert.Single(turn.Build().Ops)).Repeat);
    }

    /// <summary>A caller that records past the limit without asking is a bug, refused where it happens.</summary>
    [Fact]
    public void RecordingPastTheLimitThrows()
    {
        var builder = new OrderDocumentBuilder(Local);
        for (var index = 0; index < OrderDocumentBuilder.MaxOps; index++) builder.DismissNotification(Local);

        Assert.False(builder.HasRoom);
        Assert.Throws<InvalidOperationException>(() => builder.DismissNotification(Local));
        Assert.Equal(OrderDocumentBuilder.MaxOps, builder.Build().Ops.Count);
    }

    /// <summary>
    /// Orders, re-orders, cancellations, Moves and hire changes over every gang the seat has,
    /// recorded in <paramref name="log"/> exactly as the planning copy accepted them.
    /// </summary>
    private static void PlayABusyTurn(SpeculativeTurn turn, OrderDocumentBuilder log, GangId carried)
    {
        void Submit(GameCommand command)
        {
            if (turn.Submit(command).Accepted) log.Submit(command);
        }

        void Cancel(GangId gang)
        {
            if (turn.Cancel(gang).Accepted) log.Cancel(Local, gang);
        }

        var gangs = ActiveGangs(turn.State);
        foreach (var gang in gangs)
        {
            Submit(Hide(gang));
            foreach (var neighbour in Neighbours(turn.State.FindGang(gang)!.SectorId))
                Submit(new GameCommand(Local, gang, GangAction.Move, CoreTarget.Sector(neighbour)));
            Submit(Hide(gang, repeat: true));
        }
        // The first gang ends on a Move, the second with its order withdrawn, the carried one
        // without the order it came in with.
        foreach (var neighbour in Neighbours(turn.State.FindGang(gangs[0])!.SectorId).Take(1))
            Submit(new GameCommand(Local, gangs[0], GangAction.Move, CoreTarget.Sector(neighbour)));
        if (gangs.Count > 2) Cancel(gangs[1]);
        Cancel(carried);

        var (offers, sector) = HireChoices(turn.State);
        foreach (var offer in offers)
        {
            if (turn.QueueHire(offer, sector).Accepted) log.QueueHire(Local, offer, sector);
        }
        var snub = turn.SnubHireOffer(offers[0]);
        if (snub.Accepted) log.SnubHireOffer(Local, offers[0], snub.GangDefinitionId is null);
        snub = turn.SnubHireOffer(offers[0]);
        if (snub.Accepted) log.SnubHireOffer(Local, offers[0], snub.GangDefinitionId is null);
        if (turn.QueueHire(offers[^1], sector).Accepted) log.QueueHire(Local, offers[^1], sector);

        while (turn.DismissNotification()) log.DismissNotification(Local);
    }

    /// <summary>
    /// A client on the Command phase of a turn in which the local seat holds three gangs: the
    /// match starts it with one, so it hires the cheapest offer through sealed turns until then.
    /// </summary>
    private static (MatchReplayRecorder Replay, OriginalData Definitions) NewClient()
    {
        var definitions = BundledOriginalData.Load();
        var state = MatchBootstrapFactory.Create(definitions, Seed, Settings, Roster);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);
        for (var turn = 0; turn < 8 && ActiveGangCount(replay.State) < 3; turn++)
        {
            var seat = replay.State.FindPlayer(Local)!;
            var offer = seat.HirePool
                .OrderBy(id => HireRules.InitialCost(definitions.Gangs.Single(gang => gang.Id == id)))
                .First();
            var hire = new OrderDocumentBuilder(Local);
            hire.QueueHire(Local, offer, replay.State.Sectors.First(sector => sector.Owner == Local).Id);
            SealedTurnApplier.Apply(replay, Sealed(replay.State, hire.Build()));
        }
        Assert.True(ActiveGangCount(replay.State) >= 3, "the fixture seat hires up to three gangs");
        return (replay, definitions);
    }

    private static int ActiveGangCount(MatchState state) =>
        state.FindPlayer(Local)!.Gangs.Count(gang => gang.IsActive);

    private static SealedOrdersView Sealed(MatchState state, OrderDocument local)
    {
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        SealedPlayerOrders[] players =
        [
            new("p1", 0, local, OrderDigest.OfDocument(local)),
            new("p2", 1, empty, OrderDigest.OfDocument(empty)),
        ];
        return new SealedOrdersView(state.Coordinator.Turn, OrderDigest.OfSet(players), players);
    }

    private static IReadOnlyList<GangId> ActiveGangs(MatchState state)
    {
        var gangs = state.FindPlayer(Local)!.Gangs.Where(gang => gang.IsActive).Select(gang => gang.Id).ToArray();
        Assert.True(gangs.Length >= 3, "the fixture seat needs three gangs");
        return gangs;
    }

    private static (IReadOnlyList<short> Offers, int Sector) HireChoices(MatchState state)
    {
        var player = state.FindPlayer(Local)!;
        var offers = player.HirePool.Distinct().ToArray();
        Assert.True(offers.Length >= 2, "the fixture seat needs two distinct offers");
        return (offers, state.Sectors.First(sector => sector.Owner == Local).Id);
    }

    private static IEnumerable<int> Neighbours(int sector)
    {
        var (row, column) = Math.DivRem(sector, MatchLimits.BoardWidth);
        if (row > 0) yield return sector - MatchLimits.BoardWidth;
        if (column > 0) yield return sector - 1;
        if (column < MatchLimits.BoardWidth - 1) yield return sector + 1;
        if (row < MatchLimits.BoardWidth - 1) yield return sector + MatchLimits.BoardWidth;
    }

    private static GameCommand Hide(GangId gang, bool repeat = false) =>
        new(Local, gang, GangAction.Hide, CoreTarget.None, repeat);

    /// <summary>What the player planned: the queue in execution order and the seat's dock and inbox.</summary>
    private static string Planning(MatchState state)
    {
        var text = new StringBuilder();
        foreach (var queued in state.Commands.ExecutionPlan()) text.AppendLine(queued.Command.ToString());
        foreach (var gang in state.FindPlayer(Local)!.Gangs)
            text.AppendLine($"{gang.Id} hidden={gang.Hidden} queued={gang.QueuedCommand?.Command}");
        var seat = state.FindPlayer(Local)!;
        text.AppendLine($"cash={seat.Cash} snub={seat.SnubbedHireOffer}");
        foreach (var pending in seat.PendingHires) text.AppendLine(pending.ToString());
        text.AppendLine($"notifications={state.NotificationsFor(Local).Count}");
        return text.ToString();
    }

    /// <summary>
    /// Everything a resolved turn decides, leaving out the planning events and the absolute command
    /// sequence numbers, the only things compaction changes.
    /// </summary>
    private static string Resolved(MatchState state)
    {
        var text = new StringBuilder();
        text.AppendLine($"turn={state.Coordinator.Turn} phase={state.Coordinator.Phase}");
        text.AppendLine($"random={state.Random.State}/{state.Random.ConsumptionCount}");
        foreach (var player in state.Players)
        {
            text.AppendLine($"{player.Id} cash={player.Cash} status={player.Status} "
                + $"pool={string.Join(',', player.HirePool)}");
            foreach (var gang in player.Gangs)
            {
                text.AppendLine($"  {gang.Id} {gang.DefinitionId} active={gang.IsActive} sector={gang.SectorId} "
                    + $"force={gang.Force} hidden={gang.Hidden} queued={gang.QueuedCommand?.Command}");
            }
        }
        foreach (var sector in state.Sectors) text.Append(sector.Owner?.Value ?? -1).Append(' ');
        text.AppendLine();
        foreach (var queued in state.Commands.ExecutionPlan()) text.AppendLine(queued.Command.ToString());
        return text.ToString();
    }
}

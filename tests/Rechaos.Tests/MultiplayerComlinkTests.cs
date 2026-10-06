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
/// Comlink in an online match: a message and a read mark travel as order ops and every client
/// applies them when the turn seals (RULE-COMLINK-001, RULE-COMLINK-003, RULE-COMLINK-005,
/// RULE-COMLINK-007). A message reaches its recipients at the seal rather than at once, the
/// departure DEV-NET-001 records.
/// </summary>
public sealed class MultiplayerComlinkTests
{
    private const int Seed = 1996;

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
        new("p3", 2, "LINUS", PortraitId: 2, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    private static readonly PlayerId Ada = new(0);
    private static readonly PlayerId Grace = new(1);
    private static readonly PlayerId Linus = new(2);

    [Fact]
    public void TwoClientsApplyingTheSameComlinkOpsAgreeOnEveryInbox()
    {
        var (left, definitions) = NewClient();
        var (right, _) = NewClient();
        var ada = SpeculativeTurn.For(left.State, definitions, 0);
        var linus = SpeculativeTurn.For(left.State, definitions, 2);
        Assert.True(ada.SendComlinkMessage([Linus, Grace], "MEET AT DAWN.").Accepted);
        Assert.True(linus.SendComlinkMessage([Ada], "NO.").Accepted);
        var orders = Sealed(1, (0, ada.Build()), (2, linus.Build()));

        var leftHash = SealedTurnApplier.Apply(left, orders);
        var rightHash = SealedTurnApplier.Apply(right, orders);

        Assert.Equal(leftHash, rightHash);
        foreach (var client in new[] { left.State, right.State })
        {
            var toGrace = Assert.Single(client.ComlinkFor(Grace).Messages);
            Assert.Equal((Ada, 1, "MEET AT DAWN."), (toGrace.Sender, toGrace.Turn, toGrace.Text));
            Assert.Equal("MEET AT DAWN.", Assert.Single(client.ComlinkFor(Linus).Messages).Text);
            Assert.Equal("NO.", Assert.Single(client.ComlinkFor(Ada).Messages).Text);
            Assert.True(client.ComlinkFor(Grace).HasUnread);
        }
    }

    /// <summary>
    /// DEV-NET-001: a player plans on the state the turn started from, so a message from a lower
    /// slot is not in the recipient's copy during the turn it was sent in. It is in every client's
    /// state once the turn seals, unread.
    /// </summary>
    [Fact]
    public void AMessageReachesItsRecipientWhenTheTurnSeals()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0);
        var grace = SpeculativeTurn.For(replay.State, definitions, 1);
        Assert.True(ada.SendComlinkMessage([Grace], "HELLO").Accepted);

        Assert.Empty(grace.State.ComlinkFor(Grace).Messages);

        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build()), (1, grace.Build())));
        var message = Assert.Single(replay.State.ComlinkFor(Grace).Messages);
        Assert.False(replay.State.ComlinkFor(Grace).IsRead(message.Sequence));
    }

    /// <summary>
    /// RULE-COMLINK-005 and RULE-COMLINK-007: reading marks the reader's own copy at once, and the
    /// mark travels in the orders, so when the turn seals the read message at the front of the
    /// inbox is dropped as the reader's planning ends, on every client.
    /// </summary>
    [Fact]
    public void AReadMarkShowsAtOnceAndTheReadMessageIsDroppedAtTheSeal()
    {
        var (left, definitions) = NewClient();
        var (right, _) = NewClient();
        var ada = SpeculativeTurn.For(left.State, definitions, 0);
        Assert.True(ada.SendComlinkMessage([Grace], "FIRST").Accepted);
        Assert.True(ada.SendComlinkMessage([Grace], "SECOND").Accepted);
        var turnOne = Sealed(1, (0, ada.Build()));
        SealedTurnApplier.Apply(left, turnOne);
        SealedTurnApplier.Apply(right, turnOne);
        var first = left.State.ComlinkFor(Grace).Messages[0];

        var grace = SpeculativeTurn.For(left.State, definitions, 1);
        Assert.True(grace.MarkComlinkRead(first.Sequence));
        Assert.True(grace.State.ComlinkFor(Grace).IsRead(first.Sequence));
        Assert.False(left.State.ComlinkFor(Grace).IsRead(first.Sequence));
        Assert.False(grace.MarkComlinkRead(first.Sequence));
        var read = Assert.IsType<MarkComlinkReadOp>(Assert.Single(grace.Build().Ops));
        Assert.Equal((1, (int)first.Sequence), (read.Player, read.Sequence));

        var turnTwo = Sealed(2, (1, grace.Build()));
        var leftHash = SealedTurnApplier.Apply(left, turnTwo);
        var rightHash = SealedTurnApplier.Apply(right, turnTwo);

        Assert.Equal(leftHash, rightHash);
        Assert.Equal("SECOND", Assert.Single(left.State.ComlinkFor(Grace).Messages).Text);
    }

    /// <summary>
    /// RULE-COMLINK-002 and RULE-COMLINK-003 on the planning copy: a refused send and a draft of
    /// spaces only both leave the orders empty, and the refusal is the core's own.
    /// </summary>
    [Fact]
    public void OnlyASendTheCoreAcceptsWithRecipientsIsRecorded()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0);

        var toSelf = ada.SendComlinkMessage([Ada], "ME");
        var toComputer = ada.SendComlinkMessage([new PlayerId(4)], "BOT");
        var blank = ada.SendComlinkMessage([Grace], "   ");

        Assert.Equal(ComlinkValidationCode.SenderIsRecipient, toSelf.Code);
        Assert.Equal(ComlinkValidationCode.RecipientNotHuman, toComputer.Code);
        Assert.True(blank.Accepted);
        Assert.Empty(ada.Build().Ops);
    }

    /// <summary>
    /// A document a peer could have written but the rules refuse is refused the same way on every
    /// client: a message to a computer player, a read mark on a message nobody has. The hash says so.
    /// </summary>
    [Fact]
    public void OpsTheRulesRefuseAreRefusedIdenticallyEverywhere()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();
        var document = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion,
        [
            new SendComlinkMessageOp(0, [4], "TO THE MACHINE"),
            new SendComlinkMessageOp(0, [0], "TO MYSELF"),
            new MarkComlinkReadOp(0, 40),
        ]);
        var orders = Sealed(1, (0, document));

        Assert.Equal(SealedTurnApplier.Apply(left, orders), SealedTurnApplier.Apply(right, orders));
        Assert.All(left.State.Players, player => Assert.Empty(left.State.ComlinkFor(player.Id).Messages));
    }

    [Fact]
    public void ARestoredDraftRebuildsTheReadMarksAndTheSentMessages()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0);
        Assert.True(ada.SendComlinkMessage([Grace], "PING").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));
        var grace = SpeculativeTurn.For(replay.State, definitions, 1);
        var sequence = replay.State.ComlinkFor(Grace).Messages[0].Sequence;
        Assert.True(grace.MarkComlinkRead(sequence));
        Assert.True(grace.SendComlinkMessage([Ada, Linus], "PONG").Accepted);

        var restored = SpeculativeTurn.Restore(replay.State, definitions, 1, grace.Build());

        Assert.True(restored.State.ComlinkFor(Grace).IsRead(sequence));
        Assert.Equal("PONG", Assert.Single(restored.State.ComlinkFor(Linus).Messages).Text);
        Assert.Equal(
            OrderDigest.OfDocument(grace.Build()),
            OrderDigest.OfDocument(restored.Build()));
    }

    /// <summary>
    /// The journal a client keeps of the sealed turns replays to the same state, inboxes included,
    /// and the snapshot form of the state round-trips through the native save.
    /// </summary>
    [Fact]
    public void ASealedTurnWithComlinkOpsReplaysAndRoundTripsTheSnapshot()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0);
        Assert.True(ada.SendComlinkMessage([Grace, Linus], "ALL OF US").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));
        var grace = SpeculativeTurn.For(replay.State, definitions, 1);
        Assert.True(grace.MarkComlinkRead(replay.State.ComlinkFor(Grace).Messages[0].Sequence));
        var hash = SealedTurnApplier.Apply(replay, Sealed(2, (1, grace.Build())));

        using var journal = new MemoryStream();
        MatchReplaySerializer.Save(journal, replay);
        journal.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(journal, definitions);
        var snapshot = MatchStateClone.FromBase64(MatchStateClone.ToBase64(replay.State), definitions);

        Assert.Equal(hash, MatchStateHasher.ComputeFingerprint(replayed));
        Assert.Equal(hash, MatchStateHasher.ComputeFingerprint(snapshot));
        Assert.Empty(snapshot.ComlinkFor(Grace).Messages);
        Assert.Equal("ALL OF US", Assert.Single(snapshot.ComlinkFor(Linus).Messages).Text);
    }

    /// <summary>
    /// A seat whose human sent messages is voted onto the computer at a turn boundary, as every
    /// client does on the approved takeover. The messages stay with their recipients, the next
    /// sealed turn resolves, and the snapshot of that state still loads.
    /// </summary>
    [Fact]
    public void ATakeoverOfASeatThatSentMessagesKeepsThemAndTheSnapshotLoads()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0);
        Assert.True(ada.SendComlinkMessage([Grace], "GOING AWAY").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));

        Assert.True(replay.TransferPlayerToComputer(Ada));
        var hash = SealedTurnApplier.Apply(replay, Sealed(2));
        var snapshot = MatchStateClone.FromBase64(MatchStateClone.ToBase64(replay.State), definitions);

        Assert.Equal(Ada, Assert.Single(replay.State.ComlinkFor(Grace).Messages).Sender);
        Assert.Equal(PlayerController.Computer, snapshot.Setup.Players[0].Controller);
        Assert.Equal(hash, MatchStateHasher.ComputeFingerprint(snapshot));
        // RULE-COMLINK-002: a seat the computer plays no longer takes messages.
        Assert.False(snapshot.IsComlinkRecipient(Grace, Ada));
    }

    [Theory]
    [InlineData("lower case")]
    [InlineData("TAB\tHERE")]
    [InlineData("CAFÉ")]
    [InlineData("")]
    public void TheDecoderRefusesTextTheSendPanelCouldNotHaveWritten(string text) =>
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [1], text), Ada, "the sealed set"));

    [Fact]
    public void TheDecoderRefusesRecipientsAndSequencesNoBoardHas()
    {
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [6], "HI"), Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [], "HI"), Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [1, 2, 3, 4, 5, 0], "HI"), Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new MarkComlinkReadOp(0, -1), Ada, "the sealed set"));
        var decoded = Assert.IsType<DecodedOrderOp.SendComlinkMessage>(OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [2, 1], new string('Z', MatchLimits.ComlinkMessageCharacters)),
            Ada, "the sealed set"));
        Assert.Equal([Linus, Grace], decoded.Recipients);
    }

    [Fact]
    public void TheBuilderRecordsOnlyItsOwnSeat()
    {
        var builder = new OrderDocumentBuilder(Ada);

        Assert.Throws<InvalidOperationException>(() => builder.SendComlinkMessage(Grace, [Ada], "HI"));
        Assert.Throws<InvalidOperationException>(() => builder.MarkComlinkRead(Grace, 0));
        builder.SendComlinkMessage(Ada, [Grace], "HI");
        builder.MarkComlinkRead(Ada, 3);

        Assert.Equal(
            [new SendComlinkMessageOp(0, [1], "HI").Op, new MarkComlinkReadOp(0, 3).Op],
            builder.Build().Ops.Select(op => op.Op));
    }

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
}

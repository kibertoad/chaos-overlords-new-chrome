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
/// RULE-COMLINK-007). A message reaches its recipients at the seal rather than at once, and it
/// travels and is stored sealed for each recipient, so only that recipient's client can read it:
/// the departures DEV-NET-001 records.
/// </summary>
public sealed class MultiplayerComlinkTests
{
    private const int Seed = 1996;
    private const string MatchId = "match-comlink";

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly ComlinkKeyPair[] Keys =
        [ComlinkKeyPair.Generate(), ComlinkKeyPair.Generate(), ComlinkKeyPair.Generate()];

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true, ComlinkKey: Keys[0].PublicKey),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false, ComlinkKey: Keys[1].PublicKey),
        new("p3", 2, "LINUS", PortraitId: 2, Status: WirePlayerStatus.Active, IsHost: false, ComlinkKey: Keys[2].PublicKey),
    ];

    private static readonly PlayerId Ada = new(0);
    private static readonly PlayerId Grace = new(1);
    private static readonly PlayerId Linus = new(2);

    /// <summary>
    /// Every client stores the same envelopes and agrees on the hash, and each recipient's client,
    /// and only it, opens its own copy. Nothing in the orders or in any inbox carries the text.
    /// </summary>
    [Fact]
    public void TwoClientsAgreeOnEveryInboxAndOnlyEachRecipientCanReadItsMessage()
    {
        var (left, definitions) = NewClient();
        var (right, _) = NewClient();
        var ada = SpeculativeTurn.For(left.State, definitions, 0, Keyring(Ada));
        var linus = SpeculativeTurn.For(left.State, definitions, 2, Keyring(Linus));
        Assert.True(ada.SendComlinkMessage([Linus, Grace], "MEET AT DAWN.").Accepted);
        Assert.True(linus.SendComlinkMessage([Ada], "NO.").Accepted);
        var orders = Sealed(1, (0, ada.Build()), (2, linus.Build()));

        var leftHash = SealedTurnApplier.Apply(left, orders);
        var rightHash = SealedTurnApplier.Apply(right, orders);

        Assert.Equal(leftHash, rightHash);
        Assert.DoesNotContain("DAWN", OrderDigest.CanonicalTextOf(ada.Build()), StringComparison.Ordinal);
        Assert.DoesNotContain("NO.", OrderDigest.CanonicalTextOf(linus.Build()), StringComparison.Ordinal);
        foreach (var client in new[] { left.State, right.State })
        {
            var toGrace = Assert.Single(client.ComlinkFor(Grace).Messages);
            var toLinus = Assert.Single(client.ComlinkFor(Linus).Messages);
            var toAda = Assert.Single(client.ComlinkFor(Ada).Messages);
            Assert.Equal((Ada, 1, string.Empty, true), (toGrace.Sender, toGrace.Turn, toGrace.Text, toGrace.IsSealed));
            Assert.NotEqual(toGrace.Envelope, toLinus.Envelope);
            Assert.Equal("MEET AT DAWN.", Keyring(Grace).Read(toGrace));
            Assert.Equal("MEET AT DAWN.", Keyring(Linus).Read(toLinus));
            Assert.Equal("NO.", Keyring(Ada).Read(toAda));
            // The sender, the other recipient and anyone else hold the envelope and nothing more.
            Assert.Null(Keyring(Ada).Read(toGrace));
            Assert.Null(Keyring(Linus).Read(toGrace));
            Assert.Null(Keyring(Grace).Read(toAda));
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
        var ada = SpeculativeTurn.For(replay.State, definitions, 0, Keyring(Ada));
        var grace = SpeculativeTurn.For(replay.State, definitions, 1, Keyring(Grace));
        Assert.True(ada.SendComlinkMessage([Grace], "HELLO").Accepted);

        Assert.Empty(grace.State.ComlinkFor(Grace).Messages);

        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build()), (1, grace.Build())));
        var message = Assert.Single(replay.State.ComlinkFor(Grace).Messages);
        Assert.False(replay.State.ComlinkFor(Grace).IsRead(message.Sequence));
        Assert.Equal("HELLO", grace.Comlink!.Read(message));
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
        var ada = SpeculativeTurn.For(left.State, definitions, 0, Keyring(Ada));
        Assert.True(ada.SendComlinkMessage([Grace], "FIRST").Accepted);
        Assert.True(ada.SendComlinkMessage([Grace], "SECOND").Accepted);
        var turnOne = Sealed(1, (0, ada.Build()));
        SealedTurnApplier.Apply(left, turnOne);
        SealedTurnApplier.Apply(right, turnOne);
        var first = left.State.ComlinkFor(Grace).Messages[0];

        var grace = SpeculativeTurn.For(left.State, definitions, 1, Keyring(Grace));
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
        Assert.Equal("SECOND", Keyring(Grace).Read(Assert.Single(left.State.ComlinkFor(Grace).Messages)));
    }

    /// <summary>
    /// RULE-COMLINK-002, RULE-COMLINK-003 and RULE-COMLINK-006 on the planning copy: a refused send
    /// and a draft of spaces only both leave the orders empty, and the refusal is the core's own,
    /// judged before anything is sealed. A recipient whose client has published no key cannot be
    /// sealed to, so that send is refused too.
    /// </summary>
    [Fact]
    public void OnlyASendTheCoreAcceptsAndThatCanBeSealedIsRecorded()
    {
        var (replay, definitions) = NewClient();
        var keyring = new ComlinkKeyring(MatchId, Ada, Keys[0]);
        keyring.Learn([Roster[0], Roster[1], Roster[2] with { ComlinkKey = null }]);
        var ada = SpeculativeTurn.For(replay.State, definitions, 0, keyring);
        var unkeyed = SpeculativeTurn.For(replay.State, definitions, 0);

        var toSelf = ada.SendComlinkMessage([Ada], "ME");
        var toComputer = ada.SendComlinkMessage([new PlayerId(4)], "BOT");
        var blank = ada.SendComlinkMessage([Grace], "   ");
        var noKey = ada.SendComlinkMessage([Grace, Linus], "WHO");
        var noKeyring = unkeyed.SendComlinkMessage([Grace], "WHO");

        Assert.Equal(ComlinkValidationCode.SenderIsRecipient, toSelf.Code);
        Assert.Equal(ComlinkValidationCode.RecipientNotHuman, toComputer.Code);
        // The Send panel cannot type lower case, and nothing can seal it.
        Assert.Throws<ArgumentException>(() => ada.SendComlinkMessage([Grace], "hi"));
        Assert.True(blank.Accepted);
        Assert.Equal(ComlinkValidationCode.RecipientUnreachable, noKey.Code);
        Assert.Equal(ComlinkValidationCode.RecipientUnreachable, noKeyring.Code);
        Assert.Empty(ada.Build().Ops);
        Assert.Empty(unkeyed.Build().Ops);
        Assert.All(ada.State.Players, player => Assert.Empty(ada.State.ComlinkFor(player.Id).Messages));
    }

    /// <summary>
    /// A document a peer could have written but the rules refuse is refused the same way on every
    /// client: a message to a computer player, to oneself or to one seat twice, a read mark on a
    /// message nobody has. The hash says so.
    /// </summary>
    [Fact]
    public void OpsTheRulesRefuseAreRefusedIdenticallyEverywhere()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();
        var envelope = Envelope("TO THE MACHINE", Keys[1], Grace);
        var document = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion,
        [
            new SendComlinkMessageOp(0, [new ComlinkLetter(4, envelope)]),
            new SendComlinkMessageOp(0, [new ComlinkLetter(0, envelope)]),
            new SendComlinkMessageOp(0, [new ComlinkLetter(1, envelope), new ComlinkLetter(1, envelope)]),
            new MarkComlinkReadOp(0, 40),
        ]);
        var orders = Sealed(1, (0, document));

        Assert.Equal(SealedTurnApplier.Apply(left, orders), SealedTurnApplier.Apply(right, orders));
        Assert.All(left.State.Players, player => Assert.Empty(left.State.ComlinkFor(player.Id).Messages));
    }

    /// <summary>
    /// What a modified client can do with a letter the rules accept: seal it to the wrong key, move
    /// another recipient's envelope into it, or seal it for another turn. Every client stores the
    /// same envelope and agrees on the hash; the recipient sees a message it cannot read.
    /// </summary>
    [Fact]
    public void AnEnvelopeTheRecipientCannotOpenIsStoredAlikeAndReadsAsNothing()
    {
        var (left, _) = NewClient();
        var (right, _) = NewClient();
        using var stranger = ComlinkKeyPair.Generate();
        var document = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion,
        [
            new SendComlinkMessageOp(0, [new ComlinkLetter(1, Envelope("WRONG KEY", stranger, Grace))]),
            new SendComlinkMessageOp(0, [new ComlinkLetter(1, Envelope("FOR LINUS", Keys[2], Linus))]),
            new SendComlinkMessageOp(0, [new ComlinkLetter(1, Envelope("FOR GRACE", Keys[1], Grace, turn: 7))]),
        ]);
        var orders = Sealed(1, (0, document));

        Assert.Equal(SealedTurnApplier.Apply(left, orders), SealedTurnApplier.Apply(right, orders));
        var inbox = left.State.ComlinkFor(Grace).Messages;
        Assert.Equal(3, inbox.Count);
        Assert.All(inbox, message => Assert.Null(Keyring(Grace).Read(message)));
    }

    [Fact]
    public void ARestoredDraftRebuildsTheReadMarksAndTheSentMessages()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0, Keyring(Ada));
        Assert.True(ada.SendComlinkMessage([Grace], "PING").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));
        var grace = SpeculativeTurn.For(replay.State, definitions, 1, Keyring(Grace));
        var sequence = replay.State.ComlinkFor(Grace).Messages[0].Sequence;
        Assert.True(grace.MarkComlinkRead(sequence));
        Assert.True(grace.SendComlinkMessage([Ada, Linus], "PONG").Accepted);

        var restored = SpeculativeTurn.Restore(replay.State, definitions, 1, grace.Build(), Keyring(Grace));

        Assert.True(restored.State.ComlinkFor(Grace).IsRead(sequence));
        Assert.Equal("PONG", Keyring(Linus).Read(Assert.Single(restored.State.ComlinkFor(Linus).Messages)));
        Assert.Equal(
            OrderDigest.OfDocument(grace.Build()),
            OrderDigest.OfDocument(restored.Build()));
    }

    /// <summary>
    /// The journal a client keeps of the sealed turns replays to the same state, inboxes included,
    /// and the snapshot form of the state round-trips through the native save. Both carry the
    /// envelopes, which the recipient still opens.
    /// </summary>
    [Fact]
    public void ASealedTurnWithComlinkOpsReplaysAndRoundTripsTheSnapshot()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0, Keyring(Ada));
        Assert.True(ada.SendComlinkMessage([Grace, Linus], "ALL OF US").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));
        var grace = SpeculativeTurn.For(replay.State, definitions, 1, Keyring(Grace));
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
        var kept = Assert.Single(snapshot.ComlinkFor(Linus).Messages);
        Assert.Equal(string.Empty, kept.Text);
        Assert.Equal("ALL OF US", Keyring(Linus).Read(kept));
        Assert.Equal(kept, Assert.Single(replayed.ComlinkFor(Linus).Messages));
    }

    /// <summary>
    /// A seat whose human sent messages is voted onto the computer at a turn boundary, as every
    /// client does on the approved takeover. The messages stay with their recipients and still
    /// open, the next sealed turn resolves, and the snapshot of that state still loads.
    /// </summary>
    [Fact]
    public void ATakeoverOfASeatThatSentMessagesKeepsThemAndTheSnapshotLoads()
    {
        var (replay, definitions) = NewClient();
        var ada = SpeculativeTurn.For(replay.State, definitions, 0, Keyring(Ada));
        Assert.True(ada.SendComlinkMessage([Grace], "GOING AWAY").Accepted);
        SealedTurnApplier.Apply(replay, Sealed(1, (0, ada.Build())));

        Assert.True(replay.TransferPlayerToComputer(Ada));
        var hash = SealedTurnApplier.Apply(replay, Sealed(2));
        var snapshot = MatchStateClone.FromBase64(MatchStateClone.ToBase64(replay.State), definitions);

        var kept = Assert.Single(snapshot.ComlinkFor(Grace).Messages);
        Assert.Equal(Ada, kept.Sender);
        Assert.Equal("GOING AWAY", Keyring(Grace).Read(kept));
        Assert.Equal(PlayerController.Computer, snapshot.Setup.Players[0].Controller);
        Assert.Equal(hash, MatchStateHasher.ComputeFingerprint(snapshot));
        // RULE-COMLINK-002: a seat the computer plays no longer takes messages.
        Assert.False(snapshot.IsComlinkRecipient(Grace, Ada));
    }

    [Theory]
    [InlineData("")]
    [InlineData("MEET AT DAWN.")]
    [InlineData("not base64 at all, and far too short to be an envelope")]
    public void TheDecoderRefusesALetterThatIsNotASealedEnvelope(string envelope) =>
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [new ComlinkLetter(1, envelope)]), Ada, "the sealed set"));

    [Fact]
    public void TheDecoderRefusesRecipientsAndSequencesNoBoardHas()
    {
        var envelope = Envelope("HI", Keys[1], Grace);
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [new ComlinkLetter(6, envelope)]), Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, []), Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [.. new[] { 1, 2, 3, 4, 5, 0 }.Select(slot => new ComlinkLetter(slot, envelope))]),
            Ada, "the sealed set"));
        Assert.Throws<MultiplayerProtocolException>(() => OrderOpDecoder.Decode(
            new MarkComlinkReadOp(0, -1), Ada, "the sealed set"));
        var decoded = Assert.IsType<DecodedOrderOp.SendComlinkMessage>(OrderOpDecoder.Decode(
            new SendComlinkMessageOp(0, [new ComlinkLetter(2, envelope), new ComlinkLetter(1, envelope)]),
            Ada, "the sealed set"));
        Assert.Equal([Linus, Grace], decoded.Letters.Select(letter => letter.Recipient));
        Assert.All(decoded.Letters, letter => Assert.Equal(envelope, letter.Envelope));
    }

    [Fact]
    public void TheBuilderRecordsOnlyItsOwnSeat()
    {
        var builder = new OrderDocumentBuilder(Ada);
        SealedComlinkLetter[] letters = [new(Grace, Envelope("HI", Keys[1], Grace))];

        Assert.Throws<InvalidOperationException>(() => builder.SendComlinkMessage(Grace, letters));
        Assert.Throws<InvalidOperationException>(() => builder.MarkComlinkRead(Grace, 0));
        builder.SendComlinkMessage(Ada, letters);
        builder.MarkComlinkRead(Ada, 3);

        var ops = builder.Build().Ops;
        var send = Assert.IsType<SendComlinkMessageOp>(ops[0]);
        Assert.Equal(new ComlinkLetter(1, letters[0].Envelope), Assert.Single(send.Letters));
        Assert.IsType<MarkComlinkReadOp>(ops[1]);
    }

    private static ComlinkKeyring Keyring(PlayerId seat)
    {
        var keyring = new ComlinkKeyring(MatchId, seat, Keys[seat.Value]);
        keyring.Learn(Roster);
        return keyring;
    }

    private static string Envelope(string text, ComlinkKeyPair to, PlayerId recipient, int turn = 1) =>
        ComlinkSeal.Seal(text, to.PublicKey, new ComlinkSealContext(MatchId, turn, Ada, recipient));

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

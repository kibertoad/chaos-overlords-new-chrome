using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>The session's smaller parts, each of which was pulled out to be tested on its own.</summary>
public sealed partial class MultiplayerSessionTests
{
    private static (MatchReplayRecorder Replay, OriginalData Definitions) FreshMatch()
    {
        var definitions = BundledOriginalData.Load();
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        return (replay, definitions);
    }

    private static SealedOrdersView SealedWith(int turn, params OrderOp[] ops)
    {
        var document = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, ops);
        SealedPlayerOrders[] players =
        [
            new("p1", 0, document, OrderDigest.OfDocument(document)),
        ];
        return new SealedOrdersView(turn, OrderDigest.OfSet(players), players);
    }

    /// <summary>
    /// The connection is back only when every loop is back, and each failure is its own line.
    /// </summary>
    [Fact]
    public void ConnectionIsBackOnlyWhenEveryLaneIs()
    {
        var notices = new List<(bool Connected, string? Detail, int Attempt)>();
        var health = new ConnectionHealth((connected, detail, attempt) => notices.Add((connected, detail, attempt)));
        var stream = health.Open("stream");
        var outbox = health.Open("outbox");

        stream.Failed("stream down", 1);
        outbox.Failed("outbox down", 1);
        outbox.Recovered();

        Assert.False(health.IsConnected);
        Assert.Equal([(false, "stream down", 1), (false, "outbox down", 1)], notices);

        stream.Recovered();
        stream.Recovered();

        Assert.True(health.IsConnected);
        Assert.Equal((true, null, 0), Assert.Single(notices.Skip(2)));
    }

    /// <summary>The builder's version moves with every change and nothing else.</summary>
    [Fact]
    public void OrderDocumentVersionCountsEveryChange()
    {
        var (replay, _) = FreshMatch();
        var builder = new OrderDocumentBuilder(new PlayerId(0));
        var gang = replay.State.Players[0].Gangs[0].Id;

        Assert.Equal(0, builder.Version);
        builder.Cancel(new PlayerId(0), gang);
        Assert.Equal(1, builder.Version);
        builder.Build();
        Assert.Equal(1, builder.Version);
        builder.DismissNotification(new PlayerId(0));
        builder.Clear();
        Assert.Equal(3, builder.Version);
    }

    /// <summary>A sealed set for another turn than the one the match is on cannot be applied to it.</summary>
    [Fact]
    public void ASealedSetForAnotherTurnIsRefusedBeforeItTouchesTheState()
    {
        var (replay, _) = FreshMatch();
        var before = MatchStateHasher.ComputeSha256(replay.State);

        var failure = Assert.Throws<MultiplayerProtocolException>(
            () => SealedTurnApplier.Apply(replay, SealedOrders(2)));

        Assert.Contains("turn 2", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, MatchStateHasher.ComputeSha256(replay.State));
    }

    /// <summary>A value that does not fit the core is refused by the name of the field it was in.</summary>
    [Fact]
    public void AnOpThisBuildCannotReadIsRefusedByFieldName()
    {
        var (replay, definitions) = FreshMatch();
        var gang = replay.State.Players[0].Gangs[0].Id.Value;
        var badAction = new SubmitCommandOp(0, gang, 99, new NoneTarget(), false, null);
        var badHire = new QueueHireOp(0, 100_000, 0);

        var sealedFailure = Assert.Throws<MultiplayerProtocolException>(
            () => SealedTurnApplier.Apply(replay, SealedWith(1, badAction)));
        var draftFailure = Assert.Throws<MultiplayerProtocolException>(
            () => SpeculativeTurn.Restore(
                replay.State,
                definitions,
                0,
                new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, [badHire])));

        Assert.Contains("the sealed set carries action 99", sealedFailure.Message, StringComparison.Ordinal);
        Assert.Contains("the saved draft names gang definition 100000", draftFailure.Message, StringComparison.Ordinal);
    }
}

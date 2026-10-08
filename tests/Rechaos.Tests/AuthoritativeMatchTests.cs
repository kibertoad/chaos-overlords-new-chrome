using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Resolution;
using Rechaos.Multiplayer.Session;
using Xunit;
using CoreTarget = Rechaos.Core.GameModel.CommandTarget;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// The resolver the coordination server would run (docs/MULTIPLAYER.md, "Resolving turns on the
/// server") reaches the hash a client reports, event for event, picks a match up from a snapshot
/// only at the hash it is stored under, and folds a log over a snapshot as a reconnecting client
/// does (<see cref="MatchHistory"/>).
/// </summary>
/// <remarks>
/// tools/ResolverDeterminism holds the WebAssembly build of the same class to the hashes these
/// native runs produce.
/// </remarks>
public sealed class AuthoritativeMatchTests
{
    private const int Seed = 1996;
    private const string MatchId = "match";

    private static readonly MultiplayerGameSettings Settings = new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);

    private static readonly IReadOnlyList<PlayerView> Roster =
    [
        new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
        new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
    ];

    private static readonly OriginalData Definitions = BundledOriginalData.Load();

    private static MatchReplayRecorder NewClient()
    {
        var replay = new MatchReplayRecorder(MatchBootstrapFactory.Create(Definitions, Seed, Settings, Roster));
        CommandPhase.Enter(replay);
        return replay;
    }

    private static AuthoritativeMatch NewResolver() =>
        AuthoritativeMatch.Bootstrap(Definitions, Seed, Settings, Roster);

    /// <summary>
    /// Every human seat that can still act hides its first active gang, through the same copy the
    /// interface plans on, so each sealed set carries a real command for the rules to judge.
    /// </summary>
    private static SealedOrdersView Seal(MatchState state)
    {
        var entries = new List<SealedPlayerOrders>();
        foreach (var player in Roster)
        {
            var seat = state.FindPlayer(new PlayerId(player.Slot));
            if (seat is null || seat.Setup.Controller != PlayerController.Human) continue;
            var turn = SpeculativeTurn.For(state, Definitions, player.Slot);
            if (turn.State.FindPlayer(new PlayerId(player.Slot))!.Gangs.FirstOrDefault(g => g.IsActive) is { } gang)
                turn.Submit(new GameCommand(new PlayerId(player.Slot), gang.Id, GangAction.Hide, CoreTarget.None));
            var document = turn.Orders.Build();
            entries.Add(new SealedPlayerOrders(player.Id, player.Slot, document, OrderDigest.OfDocument(document)));
        }
        return new SealedOrdersView(state.Coordinator.Turn, OrderDigest.OfSet(entries), entries);
    }

    /// <summary>A match's log as the server stores it, with the sealed set of every seal.</summary>
    private sealed class Log
    {
        public List<(MatchEvent Event, SealedOrdersView? Set)> Entries { get; } = [];

        public TurnSealedEvent Sealed(SealedOrdersView set) =>
            Add(new TurnSealedEvent(Next, MatchId, "t", new TurnSealedEventPayload(set.Turn, set.OrderSetHash)), set);

        public MatchPlayerTakenOverEvent TakenOver(string playerId) =>
            Add(new MatchPlayerTakenOverEvent(Next, MatchId, "t", new MatchPlayerTakenOverEventPayload(playerId)));

        public MatchPlayerReturnedEvent Returned(string playerId) =>
            Add(new MatchPlayerReturnedEvent(Next, MatchId, "t", new MatchPlayerReturnedEventPayload(playerId, true)));

        public TurnReadinessEvent Ready(int turn, string playerId) =>
            Add(new TurnReadinessEvent(Next, MatchId, "t", new TurnReadinessEventPayload(turn, playerId, true)));

        private int Next => Entries.Count + 1;

        private T Add<T>(T @event, SealedOrdersView? set = null)
            where T : MatchEvent
        {
            Entries.Add((@event, set));
            return @event;
        }
    }

    /// <summary>
    /// Plays four turns on a client and through the resolver: slot 1 goes to the computer before
    /// turn 2 and comes back before turn 4. Returns the snapshot the resolver took after turn 3,
    /// before the return.
    /// </summary>
    private static (MatchReplayRecorder Client, Log Log, AuthoritativeMatch Resolver, string Archive, string ArchiveHash)
        PlayFourTurns()
    {
        var client = NewClient();
        var resolver = NewResolver();
        var log = new Log();
        string? archive = null;
        string? archiveHash = null;
        for (var turn = 1; turn <= 4; turn++)
        {
            if (turn == 2)
            {
                SeatControl.HandOver(client, 1, PlayerController.Computer);
                // The handover changes the state between seals, so the hash must not be a cached one.
                Assert.Equal(MatchStateHasher.ComputeFingerprint(client.State), resolver.Apply(log.TakenOver("p2")));
            }
            if (turn == 4)
            {
                archive = resolver.Snapshot();
                archiveHash = resolver.StateHash;
                SeatControl.HandOver(client, 1, PlayerController.Human);
                // The handover changes the state between seals, so the hash must not be a cached one.
                Assert.Equal(MatchStateHasher.ComputeFingerprint(client.State), resolver.Apply(log.Returned("p2")));
            }
            resolver.Apply(log.Ready(turn, "p1"));
            var set = Seal(client.State);
            var expected = SealedTurnApplier.Apply(client, set);
            Assert.Equal(expected, resolver.Apply(log.Sealed(set), set));
        }
        return (client, log, resolver, archive!, archiveHash!);
    }

    [Fact]
    public void ReachesTheHashAClientReportsEventForEvent()
    {
        var (client, _, resolver, _, _) = PlayFourTurns();

        Assert.Equal(MatchStateHasher.ComputeFingerprint(client.State), resolver.StateHash);
        Assert.Equal(5, resolver.Turn);
        Assert.Equal(PlayerController.Human, client.State.FindPlayer(new PlayerId(1))!.Setup.Controller);
    }

    [Fact]
    public void FoldsTheWholeLogOverASnapshotAsAReconnectingClientDoes()
    {
        var (client, log, _, archive, archiveHash) = PlayFourTurns();

        // From the log's start: the seals the snapshot holds need no set and are passed over, and so
        // is the takeover before turn 2, which the snapshot already reflects.
        var fromStart = AuthoritativeMatch.FromSnapshot(Definitions, archive, archiveHash, Roster, logTurn: 1);
        foreach (var (@event, set) in log.Entries)
            fromStart.Apply(@event, @event is TurnSealedEvent { Payload.Turn: < 4 } ? null : set);

        // From the last event the snapshot holds: the return and the last seal.
        var fromCheckpoint = AuthoritativeMatch.FromSnapshot(Definitions, archive, archiveHash, Roster);
        foreach (var (@event, set) in log.Entries.SkipWhile(entry => entry.Event is not MatchPlayerReturnedEvent))
            fromCheckpoint.Apply(@event, set);

        var expected = MatchStateHasher.ComputeFingerprint(client.State);
        Assert.Equal(expected, fromStart.StateHash);
        Assert.Equal(expected, fromCheckpoint.StateHash);
    }

    [Fact]
    public void CarriesOnFromItsOwnSnapshotInEitherForm()
    {
        var client = NewClient();
        var resolver = NewResolver();
        var log = new Log();
        var first = Seal(client.State);
        SealedTurnApplier.Apply(client, first);
        resolver.Apply(log.Sealed(first), first);

        var fromArchive = AuthoritativeMatch.FromSnapshot(Definitions, resolver.Snapshot(), resolver.StateHash, Roster);
        var fromPayload = AuthoritativeMatch.FromSavePayload(
            Definitions, resolver.SavePayload(), resolver.StateHash, Roster);
        var second = Seal(client.State);
        var expected = SealedTurnApplier.Apply(client, second);
        var seal = log.Sealed(second);

        Assert.Equal(expected, fromArchive.Apply(seal, second));
        Assert.Equal(expected, fromPayload.Apply(seal, second));
        Assert.Equal(3, fromPayload.Turn);
    }

    [Fact]
    public void RefusesASnapshotThatDoesNotHashToTheStateItClaims()
    {
        var resolver = NewResolver();
        var claimed = new string('0', resolver.StateHash.Length);

        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSavePayload(Definitions, resolver.SavePayload(), claimed, Roster));
        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSnapshot(Definitions, resolver.Snapshot(), claimed, Roster));
        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSavePayload(Definitions, [0x7b, 0x7d], resolver.StateHash, Roster));
    }

    [Fact]
    public void RefusesASealItCannotApply()
    {
        var resolver = NewResolver();
        var before = resolver.StateHash;
        var set = Seal(NewClient().State);
        var seal = new Log().Sealed(set);

        // No set, a set whose digest is not the one the log announced, and one that does not match
        // its own digest.
        Assert.Throws<MultiplayerProtocolException>(() => resolver.Apply(seal));
        Assert.Throws<MultiplayerProtocolException>(
            () => resolver.Apply(seal with { Payload = seal.Payload with { OrderSetHash = new string('1', 64) } }, set));
        var broken = new string('0', 64);
        Assert.Throws<MultiplayerProtocolException>(
            () => resolver.Apply(
                seal with { Payload = seal.Payload with { OrderSetHash = broken } },
                set with { OrderSetHash = broken }));
        // A seal for a turn ahead of the state.
        Assert.Throws<MultiplayerProtocolException>(
            () => resolver.Apply(seal with { Payload = seal.Payload with { Turn = 2 } }, set with { Turn = 2 }));
        Assert.Equal(before, resolver.StateHash);
    }

    /// <summary>
    /// A successor turn the server seals on its deadline after the match ended is ignored, as the
    /// client session ignores it, instead of throwing inside the applier.
    /// </summary>
    [Fact]
    public void IgnoresASetSealedAfterTheMatchFinished()
    {
        var resolver = NewResolver();
        var log = new Log();
        while (!resolver.IsFinished)
        {
            var set = EmptySeal(resolver.Turn);
            resolver.Apply(log.Sealed(set), set);
        }
        var finished = resolver.StateHash;

        var late = EmptySeal(resolver.Turn);
        Assert.Equal(finished, resolver.Apply(log.Sealed(late), late));
        Assert.Equal(finished, resolver.StateHash);
    }

    /// <summary>
    /// The recorder the resolver holds its match in keeps no journal, so a long match does not grow
    /// it turn after turn, and still reaches the hash a journaling client does.
    /// </summary>
    [Fact]
    public void ARecorderWithoutAJournalReachesTheSameHashAndKeepsNoSteps()
    {
        var client = NewClient();
        var server = MatchReplayRecorder.WithoutJournal(
            MatchBootstrapFactory.Create(Definitions, Seed, Settings, Roster));
        CommandPhase.Enter(server);

        var sealedOrders = Seal(client.State);
        Assert.Equal(SealedTurnApplier.Apply(client, sealedOrders), SealedTurnApplier.Apply(server, sealedOrders));

        Assert.False(server.IsJournaling);
        Assert.Equal(0, server.StepCount);
        Assert.True(client.StepCount > 0);
        Assert.Throws<InvalidOperationException>(() => MatchReplaySerializer.Save(new MemoryStream(), server));
    }

    private static SealedOrdersView EmptySeal(int turn)
    {
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var entries = Roster
            .Select(player => new SealedPlayerOrders(player.Id, player.Slot, empty, OrderDigest.OfDocument(empty)))
            .ToArray();
        return new SealedOrdersView(turn, OrderDigest.OfSet(entries), entries);
    }

    [Fact]
    public void PlaysTheSessionVersionOfThisBuild()
    {
        Assert.Equal(MultiplayerSessionVersion.Current, AuthoritativeMatch.SessionVersion);
        Assert.Equal(NativeSaveSerializer.CurrentFormatVersion, AuthoritativeMatch.SnapshotFormatVersion);
    }
}

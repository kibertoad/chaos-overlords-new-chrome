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
/// server") reaches the hash a client reports, fact for fact, and picks a match up from a snapshot
/// only at the hash it is stored under.
/// </summary>
/// <remarks>
/// tools/ResolverDeterminism holds the WebAssembly build of the same class to the hashes these
/// native runs produce.
/// </remarks>
public sealed class AuthoritativeMatchTests
{
    private const int Seed = 1996;

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

    [Fact]
    public void ReachesTheHashAClientReportsTurnAfterTurn()
    {
        var client = NewClient();
        var resolver = NewResolver();
        Assert.Equal(MatchStateHasher.ComputeFingerprint(client.State), resolver.StateHash);

        for (var turn = 1; turn <= 4; turn++)
        {
            if (turn == 3)
            {
                // A takeover lands between two seals, and both parties apply it at the same place.
                SeatControl.HandOver(client, 1, PlayerController.Computer);
                resolver.HandOverSeat(1, PlayerController.Computer);
                Assert.Equal(MatchStateHasher.ComputeFingerprint(client.State), resolver.StateHash);
            }
            var sealedOrders = Seal(client.State);
            Assert.Equal(turn, resolver.Turn);

            Assert.Equal(SealedTurnApplier.Apply(client, sealedOrders), resolver.ApplySealedTurn(sealedOrders));
        }
        Assert.Equal(
            PlayerController.Computer,
            client.State.FindPlayer(new PlayerId(1))!.Setup.Controller);
    }

    [Fact]
    public void CarriesOnFromItsOwnSnapshotInEitherForm()
    {
        var client = NewClient();
        var resolver = NewResolver();
        var first = Seal(client.State);
        SealedTurnApplier.Apply(client, first);
        resolver.ApplySealedTurn(first);

        var fromArchive = AuthoritativeMatch.FromSnapshot(Definitions, resolver.Snapshot(), resolver.StateHash);
        var fromPayload = AuthoritativeMatch.FromSavePayload(Definitions, resolver.SavePayload(), resolver.StateHash);
        var second = Seal(client.State);
        var expected = SealedTurnApplier.Apply(client, second);

        Assert.Equal(expected, fromArchive.ApplySealedTurn(second));
        Assert.Equal(expected, fromPayload.ApplySealedTurn(second));
        Assert.Equal(3, fromPayload.Turn);
    }

    [Fact]
    public void RefusesASnapshotThatDoesNotHashToTheStateItClaims()
    {
        var resolver = NewResolver();
        var claimed = new string('0', resolver.StateHash.Length);

        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSavePayload(Definitions, resolver.SavePayload(), claimed));
        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSnapshot(Definitions, resolver.Snapshot(), claimed));
        Assert.Throws<MultiplayerProtocolException>(
            () => AuthoritativeMatch.FromSavePayload(Definitions, [0x7b, 0x7d], resolver.StateHash));
    }

    [Fact]
    public void RefusesASealedSetThatDoesNotMatchItsOwnDigest()
    {
        var resolver = NewResolver();
        var before = resolver.StateHash;
        var sealedOrders = Seal(NewClient().State) with { OrderSetHash = new string('0', 64) };

        Assert.Throws<MultiplayerProtocolException>(() => resolver.ApplySealedTurn(sealedOrders));
        Assert.Equal(before, resolver.StateHash);
    }

    [Fact]
    public void PlaysTheSessionVersionOfThisBuild()
    {
        Assert.Equal(MultiplayerSessionVersion.Current, AuthoritativeMatch.SessionVersion);
        Assert.Equal(NativeSaveSerializer.CurrentFormatVersion, AuthoritativeMatch.SnapshotFormatVersion);
    }
}

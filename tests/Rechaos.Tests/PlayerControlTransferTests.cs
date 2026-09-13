using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

public sealed class PlayerControlTransferTests
{
    [Fact]
    public void TransferIsOneWayHashedAndUpdatesEverySetupView()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var before = MatchStateHasher.ComputeSha256(match);

        Assert.True(match.TransferPlayerToComputer(new PlayerId(1)));

        Assert.Equal(PlayerController.Computer, match.Setup.Players[1].Controller);
        Assert.Equal(PlayerController.Computer, match.Players[1].Setup.Controller);
        Assert.Equal(match.Setup.Players[1], match.Players[1].Setup);
        Assert.NotEqual(before, MatchStateHasher.ComputeSha256(match));
        Assert.False(match.TransferPlayerToComputer(new PlayerId(1)));
    }

    [Fact]
    public void TransferRequiresCleanCommandBoundaryAndKnownSeat()
    {
        var match = CreateMatch();

        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(1)));
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), match.Players[0].Gangs[0].Id,
            GangAction.Hide, Rechaos.Core.GameModel.CommandTarget.None)).Accepted);
        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            match.TransferPlayerToComputer(new PlayerId(MatchLimits.PlayerCount)));
    }

    [Fact]
    public void TransferRejectsASeatWithAuthenticatedHumanComlinkHistory()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var sent = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "STILL HERE");
        Assert.True(sent.Accepted);

        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(0)));
    }

    [Fact]
    public void TransferSurvivesNativeSaveAndRecordedReplay()
    {
        var definitions = BundledOriginalData.Load();
        var recorder = new MatchReplayRecorder(CreateMatch(definitions));
        recorder.FinishUpkeep();
        Assert.True(recorder.TransferPlayerToComputer(new PlayerId(1)));

        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, recorder.State);
        save.Position = 0;
        var restoredSave = NativeSaveSerializer.Load(save, definitions);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restoredReplay = MatchReplaySerializer.LoadAndReplay(replay, definitions);

        Assert.Equal(PlayerController.Computer, restoredSave.Setup.Players[1].Controller);
        Assert.Equal(PlayerController.Computer, restoredReplay.Setup.Players[1].Controller);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(recorder.State),
            MatchStateHasher.ComputeSha256(restoredSave));
        Assert.Equal(
            MatchStateHasher.ComputeSha256(recorder.State),
            MatchStateHasher.ComputeSha256(restoredReplay));
        var step = Assert.Single(recorder.Steps,
            item => item.Kind == ReplayOperationKind.TransferPlayerToComputer);
        Assert.Equal(new PlayerId(1), step.Player);
        Assert.True(step.Accepted);
    }

    [Fact]
    public void TransferredSeatIsPlannedByEveryLockstepClient()
    {
        var definitions = BundledOriginalData.Load();
        IReadOnlyList<PlayerView> roster =
        [
            new("p1", 0, "ADA", WirePlayerStatus.Active, IsHost: true),
            new("p2", 1, "GRACE", WirePlayerStatus.Active, IsHost: false)
        ];
        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths,
            AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, 1996, settings, roster));
        CommandPhase.Enter(replay);
        Assert.True(replay.TransferPlayerToComputer(new PlayerId(1)));
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var playerZero = new SealedPlayerOrders(
            "p1", 0, empty, OrderDigest.OfDocument(empty));
        var sealedOrders = new SealedOrdersView(
            1, OrderDigest.OfSet([playerZero]), [playerZero]);

        SealedTurnApplier.Apply(replay, sealedOrders);

        Assert.Contains(replay.Steps, step =>
            step.Kind == ReplayOperationKind.PrepareAiPlanning
            && step.Player == new PlayerId(1));
        using var serialized = new MemoryStream();
        MatchReplaySerializer.Save(serialized, replay);
        serialized.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(serialized, definitions);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(replay.State),
            MatchStateHasher.ComputeSha256(restored));
    }

    private static MatchState CreateMatch(OriginalData? definitions = null)
    {
        definitions ??= BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ADA", PlayerController.Human),
            new(new PlayerId(1), "GRACE", PlayerController.Human)
        ];
        return OriginalMatchFactory.Create(
            definitions,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
    }
}

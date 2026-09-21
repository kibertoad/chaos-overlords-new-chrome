using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What happens to a gang id after the gang is gone.
/// </summary>
/// <remarks>
/// A hire reuses the roster slot of a gang that is no longer active and the slot's previous
/// occupant stops existing, while the notifications and events that named it stay where they are.
/// A save written in that state used to be unloadable, which also meant an online repair snapshot
/// taken in it was refused.
/// </remarks>
public sealed class RetiredGangIdTests
{
    [Fact]
    public void AHireReusingADeadGangsSlotStillSavesAndLoads()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Terminate, CommandTarget.None)).Accepted);
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        Assert.Equal(0, match.FindGang(new GangId(11))!.Force);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)),
            notification => notification.Gang == new GangId(11)
                && notification.Kind == GameNotificationKind.Elimination);

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        match.FinishHire(new PlayerId(0));

        var recruit = Assert.Single(match.LastHireResolutions);
        Assert.Equal(new GangId(21), recruit.Gang);
        Assert.Null(match.FindGang(new GangId(11)));
        Assert.Contains(match.NotificationsFor(new PlayerId(0)),
            notification => notification.Gang == new GangId(11));

        var restored = RoundTrip(match);

        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(
            match.NotificationsFor(new PlayerId(0)),
            restored.NotificationsFor(new PlayerId(0)));
    }

    private static MatchState RoundTrip(MatchState match)
    {
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        stream.Position = 0;
        return NativeSaveSerializer.Load(stream, match.Definitions);
    }

    /// <summary>Two gangs in one slot's reach, and an opponent so the match does not end.</summary>
    private static MatchState CreateMatch()
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
                [
                    new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5),
                    new MatchGangState(new GangId(11), new PlayerId(0), 1, 0, 5)
                ],
                hirePool: [1, 2, 3]),
            new(setups[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), 3, 63, 5)],
                hirePool: [4, 5, 6])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : id == 63 ? new PlayerId(1) : null))
            .ToArray();
        return new MatchState(definitions, setup, players, sectors);
    }
}

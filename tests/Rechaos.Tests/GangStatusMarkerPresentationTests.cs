using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangStatusMarkerPresentationTests
{
    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(false, true, false, 1)]
    [InlineData(true, false, false, 2)]
    [InlineData(true, true, false, 3)]
    [InlineData(false, false, true, 4)]
    [InlineData(false, true, true, 5)]
    [InlineData(true, false, true, 6)]
    [InlineData(true, true, true, 7)]
    public void GangStatusCombinesIdleEnemyAndIncomingStates(
        bool hasIdleGang,
        bool hasDetectedEnemyGang,
        bool hasPendingHire,
        int expectedState)
    {
        Assert.Equal(OriginalSpriteLayout.GangStatus(expectedState),
            GangStatusMarkerPresentation.Source(
                hasIdleGang, hasDetectedEnemyGang, hasPendingHire));
    }

    [Fact]
    public void IncomingOnlyUsesTheNinthNativeFrame() =>
        Assert.Equal(new(492, 227, 20, 20), OriginalSpriteLayout.IncomingGangStatus);

    [Theory]
    // RULE-UI-006: enemy adds 1, idle 2, incoming 4 while the player is present; otherwise an
    // incoming hire alone gives frame 8 and nothing gives -1.
    [InlineData(true, false, false, false, 0)]
    [InlineData(true, true, false, false, 1)]
    [InlineData(true, false, true, false, 2)]
    [InlineData(true, true, true, true, 7)]
    [InlineData(true, false, false, true, 4)]
    [InlineData(false, false, false, true, 8)]
    [InlineData(false, true, true, true, 8)]
    [InlineData(false, true, false, false, -1)]
    [InlineData(false, false, false, false, -1)]
    public void FrameFollowsTheRuleProcedure(
        bool present, bool enemy, bool idle, bool incoming, int expected) =>
        Assert.Equal(expected, GangStatusMarkerPresentation.Frame(present, enemy, idle, incoming));

    [Fact]
    public void FrameEightSurvivesOnlyWhereNoLaterSectorLacksThePlayer()
    {
        var sectors = new SectorMarkerInputs[6];
        sectors[1] = new(false, false, false, true);
        sectors[2] = new(true, false, true, false);
        sectors[3] = new(false, false, false, false);
        sectors[4] = new(false, false, false, true);
        sectors[5] = new(true, true, false, false);

        // Sector 3 lacks the player and restores the cell under sector 1's frame 8; sector 4's
        // frame 8 stays, since sector 5 is the player's.
        Assert.Equal([-1, -1, 2, -1, 8, 1], GangStatusMarkerPresentation.MapFrames(sectors));

        // Two incoming-only sectors in a row: the second restores the first before drawing.
        var pair = new SectorMarkerInputs[3];
        pair[0] = new(false, false, false, true);
        pair[1] = new(false, false, false, true);
        pair[2] = new(true, false, false, true);
        Assert.Equal([-1, 8, 4], GangStatusMarkerPresentation.MapFrames(pair));
    }

    [Fact]
    public void MapFramesReadTheMatchFromTheActivePlayersView()
    {
        var data = BundledOriginalData.Load();
        var setupPlayers = Enumerable.Range(0, 2)
            .Select(id => new MatchPlayerSetup(new PlayerId(id), $"P{id + 1}", PlayerController.Human))
            .ToArray();
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setupPlayers);
        var seer = data.Gangs.OrderByDescending(gang => gang.Stats.Detect).First().Id;
        var plain = data.Gangs.OrderBy(gang => gang.Stats.Stealth).First().Id;
        var hidden = data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First().Id;
        var weak = data.Gangs.OrderBy(gang => gang.Stats.Detect).First().Id;
        var player = new PlayerId(0);
        var enemy = new PlayerId(1);
        var busy = new MatchGangState(new GangId(1), player, seer, 0, 10)
        {
            QueuedCommand = new QueuedCommand(
                0, new GameCommand(player, new GangId(1), GangAction.Hide, CommandTarget.None))
        };
        MatchGangState[] own =
        [
            busy,
            new(new GangId(2), player, weak, 9, 10),
            // An empty slot never counts, whatever sector it names.
            new(new GangId(3), player, seer, 20, 0)
        ];
        MatchGangState[] theirs =
        [
            new(new GangId(10), enemy, plain, 0, 10),
            new(new GangId(11), enemy, hidden, 9, 10),
            new(new GangId(12), enemy, plain, 30, 10)
        ];
        var players = new[]
        {
            new MatchPlayerState(setupPlayers[0], 20, own),
            new MatchPlayerState(setupPlayers[1], 20, theirs)
        };
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ]))
            .ToArray();
        var state = new MatchState(data, setup, players, sectors);
        state.FindPlayer(player)!.AddPendingHire(new PendingHireState(plain, 63));
        state.FindPlayer(player)!.AddPendingHire(new PendingHireState(plain, 9));

        var frames = GangStatusMarkerPresentation.MapFrames(state, player);

        Assert.True(state.CanPlayerDetectGang(player, new GangId(10)));
        Assert.False(state.CanPlayerDetectGang(player, new GangId(11)));
        // A busy gang with a seen enemy: frame 1.
        Assert.Equal(1, frames[0]);
        // Gang 11 is out of sight, so the circle stays green: idle 2 plus incoming 4.
        Assert.Equal(6, frames[9]);
        Assert.Equal(-1, frames[20]);
        Assert.Equal(-1, frames[30]);
        // Sector 63 is the last sector, so nothing after it restores its frame 8.
        Assert.Equal(8, frames[63]);
        Assert.Equal(3, frames.Count(frame => frame >= 0));
    }

    /// <summary>
    /// RULE-UI-006, FND-UI-018: the markers read the gangs_seen bytes the original sets from the
    /// visibility RULE-DETECT-001 works out when planning starts, so a gang whose Detect rises
    /// during planning does not turn a circle red until the next planning entry.
    /// </summary>
    [Fact]
    public void EnemySightIsTheSnapshotTakenWhenPlanningStarts()
    {
        var data = BundledOriginalData.Load();
        var setupPlayers = Enumerable.Range(0, 2)
            .Select(id => new MatchPlayerSetup(new PlayerId(id), $"P{id + 1}", PlayerController.Human))
            .ToArray();
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setupPlayers);
        var player = new PlayerId(0);
        var definition = data.Gangs[0].Id;
        var watcher = new MatchGangState(new GangId(1), player, definition, 5, 10);
        var lurker = new MatchGangState(new GangId(10), new PlayerId(1), definition, 5, 10);
        var players = new[]
        {
            new MatchPlayerState(setupPlayers[0], 20, [watcher]),
            new MatchPlayerState(setupPlayers[1], 20, [lurker])
        };
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ]))
            .ToArray();
        var state = new MatchState(data, setup, players, sectors);
        state.FinishUpkeep();
        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        var blind = EffectiveStatistics.From(data.Gangs[0].Stats) with { Detect = 0, Stealth = 0 };
        watcher.StoredStatistics = blind;
        lurker.StoredStatistics = blind with { Stealth = 9 };
        var cache = new GangSightSnapshotCache();

        // Idle own gang, enemy out of sight: frame 2.
        Assert.Equal(2, GangStatusMarkerPresentation.MapFrames(state, player, cache.For(state, player))[5]);

        // A Detect item bought during planning lifts the gang's Detect at once.
        watcher.StoredStatistics = blind with { Detect = 20 };
        Assert.True(state.CanPlayerDetectGang(player, lurker.Id));
        Assert.Equal(2, GangStatusMarkerPresentation.MapFrames(state, player, cache.For(state, player))[5]);

        // The next snapshot, taken at the next planning entry, turns the circle red.
        cache.Clear();
        Assert.Equal(3, GangStatusMarkerPresentation.MapFrames(state, player, cache.For(state, player))[5]);
    }
}

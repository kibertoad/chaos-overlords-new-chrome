using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ChaosRangeProjectionTests
{
    [Fact]
    public void QueuedChaosRangeUsesEffectiveGangStatisticsAndMatchesCrackdownRules()
    {
        var definitions = BundledOriginalData.Load();
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]);
        var state = OriginalMatchFactory.Create(definitions, setup);
        GameplayTurnFlow.AdvanceToPlanning(new MatchReplayRecorder(state));
        var player = state.Players[0];
        var gang = player.Gangs.Single(candidate => candidate.IsActive);
        var sector = state.Sectors[gang.SectorId];
        var item = definitions.Items.First(candidate => candidate.Stats.Chaos != 0);
        gang.MiscellaneousItemId = item.Id;
        sector.Sites[0].InfluencedBy = player.Id;

        Assert.True(state.Submit(new GameCommand(
            player.Id, gang.Id, GangAction.Chaos, CommandTarget.None)).Accepted);

        var expectedDice = sector.Income + gang.Force
            + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos;
        var estimate = ChaosRangeProjection.Detail(state, player.Id, sector.Id);
        var range = estimate.Range;

        Assert.Equal(new ChaosRange(0, expectedDice), range);
        Assert.Equal(new ChaosRangeContribution(
            gang.Id, sector.Income, gang.Force,
            EffectiveStatisticsCalculator.ForGang(state, gang).Chaos,
            expectedDice, expectedDice), Assert.Single(estimate.Contributions));
        Assert.Equal(ManualRules.TriggersCrackdown(range.Maximum, sector.Tolerance),
            range.CanTriggerCrackdown(sector.Tolerance));
    }

    [Fact]
    public void ProjectionOnlyShowsTheViewingPlayersQueuedOrders()
    {
        var state = TestMatches.Create(secondPlayerHuman: true);
        GameplayTurnFlow.AdvanceToPlanning(new MatchReplayRecorder(state));
        var first = state.Players[0].Gangs.Single(candidate => candidate.IsActive);
        var second = state.Players[1].Gangs.Single(candidate => candidate.IsActive);
        second.SectorId = first.SectorId;
        state.FinishCommand(state.Players[0].Id);

        Assert.True(state.Submit(new GameCommand(
            state.Players[1].Id, second.Id, GangAction.Chaos, CommandTarget.None)).Accepted);

        Assert.Equal(new ChaosRange(0, 0),
            ChaosRangeProjection.Project(state, state.Players[0].Id, first.SectorId));
    }
}

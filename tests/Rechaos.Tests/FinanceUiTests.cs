using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class FinanceUiTests
{
    [Fact]
    public void LayoutMatchesSectorFinancialTemplate()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), FinanceLayout.Panel);
        Assert.Equal(new Rectangle(130, 143, 60, 60), FinanceLayout.Portrait);
        Assert.Equal(new Rectangle(136, 294, 49, 24), FinanceLayout.Ok);
        Assert.Equal([160, 172, 187, 202, 217, 232, 247, 271],
            Enumerable.Range(0, FinanceLayout.RowCount).Select(FinanceLayout.ValueY));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceLayout.ValueY(8));
    }

    [Fact]
    public void CityProjectionIncludesPendingHireAndQueuedBribeWithoutMutation()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var gang = player.Gangs.Single(gang => gang.IsActive);
        var before = MatchStateHasher.ComputeSha256(state);
        var baseline = FinanceProjection.Project(state, player, null);

        Assert.Equal(before, MatchStateHasher.ComputeSha256(state));
        Assert.Equal(player.Gangs.Count(candidate => candidate.IsActive), baseline.ProjectedGangCount);
        Assert.Equal(baseline.GangUpkeep + baseline.NewContracts + baseline.Equipment
            + baseline.CityOfficials + baseline.SectorTax + baseline.SiteProtection
            + baseline.ChaosEstimate, baseline.CashAdjustment);

        Assert.True(state.Submit(new GameCommand(
            player.Id, gang.Id, GangAction.Bribe, CommandTarget.None)).Accepted);
        state.PrepareHireOffers(player.Id);
        var offer = player.HirePool[0];
        Assert.True(state.QueueHire(player.Id, offer, gang.SectorId).Accepted);

        var projected = FinanceProjection.Project(state, player, null);
        var recruit = state.Definitions.Gangs.Single(definition => definition.Id == offer);
        Assert.Equal(-ManualRules.BribeCost, projected.CityOfficials);
        Assert.Equal(-HireRules.InitialCost(recruit), projected.NewContracts);
        Assert.Equal(baseline.ProjectedGangCount + 1, projected.ProjectedGangCount);
        Assert.Equal(baseline.GangUpkeep - recruit.Upkeep, projected.GangUpkeep);
    }

    [Fact]
    public void SectorProjectionExcludesOtherSectorsAndEstimatesQueuedChaos()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var gang = player.Gangs.Single(candidate => candidate.IsActive);
        Assert.True(state.Submit(new GameCommand(
            player.Id, gang.Id, GangAction.Chaos, CommandTarget.None)).Accepted);

        var local = FinanceProjection.Project(state, player, gang.SectorId);
        var sector = state.Sectors[gang.SectorId];
        var pool = sector.Income + gang.Force
            + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos;
        var expected = sector.CrackdownActive
            ? 0
            : ManualRules.ChaosIncome(pool / 3, sector.Owner == player.Id);
        Assert.Equal(expected, local.ChaosEstimate);
        Assert.Equal(1, local.ProjectedGangCount);

        var otherSector = Enumerable.Range(0, MatchLimits.SectorCount)
            .First(id => id != gang.SectorId);
        var other = FinanceProjection.Project(state, player, otherSector);
        Assert.Equal(0, other.GangUpkeep);
        Assert.Equal(0, other.ProjectedGangCount);
        Assert.Equal(0, other.ChaosEstimate);
    }

    private static MatchState CreatePlanningMatch()
    {
        var definitions = BundledOriginalData.Load();
        var setup = new MatchSetup(
            ScenarioId.Greed,
            GameDuration.SixMonths,
            1996,
            [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)]);
        var state = OriginalMatchFactory.Create(definitions, setup);
        GameplayTurnFlow.AdvanceToPlanning(new MatchReplayRecorder(state));
        return state;
    }
}

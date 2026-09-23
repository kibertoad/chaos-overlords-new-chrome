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
        Assert.Equal(new Rectangle(128, 124, 320, 209), FinanceLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), FinanceLayout.BackgroundSource);
        Assert.Equal(new Rectangle(154, 141, 64, 64), FinanceLayout.Portrait);
        Assert.Equal(new Rectangle(161, 293, 49, 22), FinanceLayout.Ok);
        Assert.Equal(394, FinanceLayout.ValueLeft);
        Assert.Equal(new Rectangle(394, 151, 24, 7), FinanceLayout.ValueField(0));
        Assert.Equal(151, FinanceLayout.ContractCountY);
        Assert.Equal(1, FinanceLayout.ContractCountWidth(9));
        Assert.Equal(2, FinanceLayout.ContractCountWidth(10));
        Assert.Equal(322, FinanceLayout.ContractCountCloseLeft(1));
        Assert.Equal(328, FinanceLayout.ContractCountCloseLeft(10));
        Assert.Equal([151, 160, 178, 196, 214, 223, 241, 268],
            Enumerable.Range(0, FinanceLayout.RowCount).Select(FinanceLayout.ValueY));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceLayout.ValueY(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceLayout.ContractCountWidth(100));
    }

    [Fact]
    public void CityProjectionIncludesPendingHireAndQueuedBribeWithoutMutation()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var gang = player.Gangs.Single(gang => gang.IsActive);
        var before = MatchStateHasher.ComputeFingerprint(state);
        var baseline = FinanceProjection.Project(state, player, null);

        Assert.Equal(before, MatchStateHasher.ComputeFingerprint(state));
        Assert.Equal(state.Sectors.Count(sector => sector.Owner == player.Id)
            * ManualRules.ControlledSectorTax, baseline.SectorTax);
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
        Assert.Equal(-ManualRules.OriginalBribeCost, projected.CityOfficials);
        Assert.Equal(-HireRules.InitialCost(recruit), projected.NewContracts);
        Assert.Equal(baseline.ProjectedGangCount + 1, projected.ProjectedGangCount);
        Assert.Equal(baseline.GangUpkeep - recruit.Upkeep, projected.GangUpkeep);
    }

    [Fact]
    public void UnspentCashListsEveryPurchaseInSubmissionOrder()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var first = player.Gangs.Single(gang => gang.IsActive);
        var second = new MatchGangState(new GangId(900), player.Id,
            first.DefinitionId, first.SectorId, 5);
        player.AddGang(second);
        var item = state.Definitions.Items[0];

        Assert.True(state.Submit(new GameCommand(player.Id, second.Id,
            GangAction.Equip, CommandTarget.Item(item.Id))).Accepted);
        Assert.True(state.Submit(new GameCommand(player.Id, first.Id,
            GangAction.Equip, CommandTarget.Item(item.Id))).Accepted);

        var spends = StatusConsolePresentation.QueuedCashSpends(state, player);
        Assert.Equal([second.Id, first.Id], spends.Select(spend => spend.Gang).ToArray());
        Assert.Equal([1, 2], spends.Select(spend => spend.Position).ToArray());
        Assert.Equal(player.Cash - spends.Sum(spend => spend.Price),
            StatusConsolePresentation.UnspentCash(state, player));

        Assert.True(state.Submit(new GameCommand(player.Id, second.Id,
            GangAction.Equip, CommandTarget.Item(item.Id))).Accepted);
        Assert.Equal([first.Id, second.Id],
            StatusConsolePresentation.QueuedCashSpends(state, player)
                .Select(spend => spend.Gang).ToArray());
    }

    [Fact]
    public void UnspentCashChargesBribesBeforeEarlierSubmittedEquips()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var briber = player.Gangs.Single(gang => gang.IsActive);
        var buyer = new MatchGangState(new GangId(900), player.Id,
            briber.DefinitionId, briber.SectorId, 5);
        player.AddGang(buyer);
        var item = state.Definitions.Items[0];

        Assert.True(state.Submit(new GameCommand(player.Id, buyer.Id,
            GangAction.Equip, CommandTarget.Item(item.Id))).Accepted);
        Assert.True(state.Submit(new GameCommand(player.Id, briber.Id,
            GangAction.Bribe, CommandTarget.None)).Accepted);

        var spends = StatusConsolePresentation.QueuedCashSpends(state, player);
        Assert.Equal([(briber.Id, GangAction.Bribe), (buyer.Id, GangAction.Equip)],
            spends.Select(spend => (spend.Gang, spend.Action)).ToArray());
        Assert.Equal("BRIBE", spends[0].Description);
        Assert.Equal(ManualRules.OriginalBribeCost, spends[0].Price);
        Assert.Equal(SpecialSiteRules.EquipmentCost(state, buyer, item), spends[1].Price);
        Assert.Equal(player.Cash - ManualRules.OriginalBribeCost - spends[1].Price,
            StatusConsolePresentation.UnspentCash(state, player));
    }

    [Fact]
    public void CashTooltipExplainsEachFigureInItsOwnSection()
    {
        var gang = new GangId(1);
        QueuedCashSpend[] spends =
        [
            new(1, gang, "TEST", GangAction.Bribe, "BRIBE", 2),
            new(2, gang, "TEST", GangAction.Equip, "PISTOL", 5)
        ];
        var projection = new FinanceProjection(
            GangUpkeep: -3, NewContracts: 0, ProjectedGangCount: 1, Equipment: -5,
            CityOfficials: -2, SectorTax: 1, SiteProtection: 0, ChaosEstimate: 4,
            CashAdjustment: -5);

        var lines = StatusConsolePresentation.CashTooltip(20, spends, projection);

        Assert.Equal("CASH  20 [13] (-5)", lines[0]);
        Assert.Equal(
            [
                "20 - CASH: MONEY ON HAND RIGHT NOW.",
                "[13] - UNSPENT: CASH LEFT AFTER QUEUED BRIBES AND EQUIPS.",
                "(-5) - DELTA: ESTIMATED CHANGE OVER THE WHOLE TURN.",
                "QUEUED SPENDING IN RESOLUTION ORDER:"
            ],
            lines.Select((line, index) => (line, index))
                .Where(entry => entry.index > 0 && lines[entry.index - 1].Length == 0)
                .Select(entry => entry.line));
        Assert.Contains("  20 CASH - 2 BRIBES - 5 EQUIPS = 13", lines);
        Assert.Contains("  GANG UPKEEP       -3", lines);
        Assert.Contains("  CHAOS ESTIMATE    +4", lines);
        Assert.Contains("  TOTAL             -5", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("  NEW CONTRACTS", StringComparison.Ordinal)
            || line.StartsWith("  SITE CASH", StringComparison.Ordinal));
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
        Assert.Equal(sector.Owner == player.Id ? ManualRules.ControlledSectorTax : 0,
            local.SectorTax);
        Assert.Equal(1, local.ProjectedGangCount);

        var otherSector = Enumerable.Range(0, MatchLimits.SectorCount)
            .First(id => id != gang.SectorId);
        var other = FinanceProjection.Project(state, player, otherSector);
        Assert.Equal(0, other.GangUpkeep);
        Assert.Equal(0, other.ProjectedGangCount);
        Assert.Equal(0, other.ChaosEstimate);
    }

    [Fact]
    public void ProjectionUsesOriginalLastSlotPayoutForQueuedMultiSell()
    {
        var state = CreatePlanningMatch();
        var player = state.Players[0];
        var gang = player.Gangs.Single(candidate => candidate.IsActive);
        var weapon = state.Definitions.Items.First(item => item.Type is >= 0 and <= 2);
        var armor = state.Definitions.Items.First(item => item.Type == 3);
        var miscellaneous = state.Definitions.Items.First(item => item.Type == 4);
        gang.WeaponItemId = weapon.Id;
        gang.ArmorItemId = armor.Id;
        gang.MiscellaneousItemId = miscellaneous.Id;
        Assert.True(state.Submit(new GameCommand(
            player.Id,
            gang.Id,
            GangAction.Sell,
            CommandTarget.Item(weapon.Id),
            SecondaryTarget: CommandTarget.Item(armor.Id),
            TertiaryTarget: CommandTarget.Item(miscellaneous.Id))).Accepted);

        var projection = FinanceProjection.Project(state, player, null);

        Assert.Equal(EquipmentRules.SaleValue(miscellaneous), projection.Equipment);
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

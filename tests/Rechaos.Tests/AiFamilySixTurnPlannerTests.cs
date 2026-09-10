using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilySixTurnPlannerTests
{
    [Fact]
    public void UncontestedGangRoutesTowardFirstUncoveredHostileHumanSector()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, targetSector: 63);
        var player = new PlayerId(0);
        BeginFamilySixTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(6, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(9), command.Target);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(9, match.AiPlanning.CoverageSector(player, 0));
    }

    [Fact]
    public void AcceptedContestedDrawAttacksAndCoversCurrentSector()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, targetSector: 0);
        var player = new PlayerId(0);
        BeginFamilySixTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(0, match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(0, match.AiPlanning.CoverageSector(player, 0));
    }

    [Theory]
    [InlineData(7, -3, GangAction.None, 0, true)]
    [InlineData(8, -3, GangAction.None, 0, false)]
    [InlineData(7, -4, GangAction.None, 0, false)]
    [InlineData(7, -3, GangAction.Attack, 0, false)]
    [InlineData(7, -3, GangAction.None, 1, false)]
    public void LiteralHealGateMatchesRecoveredHandler(
        int force,
        int heal,
        GangAction previousAction,
        int visibleWeight,
        bool expected) =>
        Assert.Equal(expected, OriginalAiFamilySixRules.ShouldHeal(
            force, heal, previousAction, visibleWeight));

    private static void BeginFamilySixTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 6);
        match.AiPlanning.SetCurrentHireRole(player, 4);
    }

    private static MatchState CreateMatch(OriginalData data, int targetSector)
    {
        var attacker = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .ThenByDescending(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        var target = data.Gangs
            .OrderBy(gang => gang.Stats.Stealth)
            .ThenBy(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, attacker.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, target.Id, targetSector, 1)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0 && targetSector == 0 ? setups[1].Id : null))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 41, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}

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

    // RULE-AI-025, FND-AI-059: when a family-6 gang already covers every weight-10 sector the
    // guard target is the end marker 100, so the move is drawn over the whole city and the
    // covered sector is left to the gang guarding it.
    [Fact]
    public void CoveredGuardTargetsLeaveTheMoveToTheTieDraw()
    {
        var data = BundledOriginalData.Load();
        var player = new PlayerId(0);
        var destinations = new HashSet<int>();
        for (var seed = 1; seed <= 20; seed++)
        {
            var match = CreateMatch(data, targetSector: 63, guardSector: 63, seed: seed);
            BeginFamilySixTurn(match, player);
            match.AiPlanning.SetFamily(player, 1, 6);
            match.AiPlanning.SetCoverageSector(player, 1, 63);
            match.FinishUpkeep();

            match.PrepareAiPlanning(player);
            var command = AiTurnPlanner.Plan(match, player)
                .Single(candidate => candidate.Gang == new GangId(10));

            Assert.Equal(GangAction.Move, command.Action);
            var destination = match.AiPlanning.CoverageSector(player, 0);
            Assert.Equal(CommandTarget.Sector(destination), command.Target);
            destinations.Add(destination);
        }

        // A route toward sector 63 always steps to 9; the tie draw also reaches row 0 or column 0.
        Assert.Contains(destinations, destination => destination != 9);
    }

    [Fact]
    public void EndMarkerModeScoresNoSector()
    {
        var owners = Enumerable.Repeat(-1, MatchLimits.SectorCount).ToArray();
        var disabled = new bool[MatchLimits.SectorCount];
        var counts = new int[MatchLimits.SectorCount];
        var destinations = new HashSet<int>();
        for (var seed = 1; seed <= 64; seed++)
            destinations.Add(OriginalAiSectorSelectionRules.Select(
                0x40 + OriginalAiSectorSelectionRules.GuardTargetEndMarker,
                27, new PlayerId(0), 6, owners, disabled, counts,
                _ => true, _ => false, _ => false, _ => false,
                [0, 1, 2, 3, 4, 5], new DeterministicRandom(seed)));

        // Every sector ties at 0, so the step heads toward a random sector in any direction.
        Assert.True(destinations.Count > 4);
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
        // RULE-AI-002: slot 0 is flagged on the first pass, so the dispatcher gives it hire role
        // 4's family and covers its sector.
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 4);
    }

    private static MatchState CreateMatch(
        OriginalData data, int targetSector, int? guardSector = null, int seed = 41)
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
            new(setups[0], 20, guardSector is { } guard
                ?
                [
                    new MatchGangState(new GangId(10), setups[0].Id, attacker.Id, 0, 10),
                    new MatchGangState(new GangId(11), setups[0].Id, attacker.Id, guard, 10)
                ]
                : [new MatchGangState(new GangId(10), setups[0].Id, attacker.Id, 0, 10)]),
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
            ScenarioId.Power, GameDuration.SixMonths, seed, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}

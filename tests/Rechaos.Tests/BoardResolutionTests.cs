using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class BoardResolutionTests
{
    [Fact]
    public void MoveChangesSectorAndRecordsOrderedResult()
    {
        var match = CreateMatch([Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)]);
        Queue(match, new GameCommand(new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1)));
        EnterMovement(match);

        match.FinishExecutionPhase();

        Assert.Equal(1, match.FindGang(new GangId(10))!.SectorId);
        var result = Assert.Single(match.LastPhaseResolutions);
        Assert.Equal(0, result.Event!.Resolution!.PreviousValue);
        Assert.Equal(1, result.Event.Resolution.ResultValue);
        Assert.Equal(GameNotificationKind.Movement, match.NotificationsFor(new PlayerId(0))[^1].Kind);
    }

    [Fact]
    public void MovingLastGangOutDoesNotAbandonControlledSector()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)], owner: new PlayerId(0));
        Queue(match, new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1)));
        EnterMovement(match);

        match.FinishExecutionPhase();

        Assert.Equal(1, match.FindGang(new GangId(10))!.SectorId);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.DoesNotContain(match.Players[0].Gangs,
            gang => gang.IsActive && gang.SectorId == 0);
    }

    [Fact]
    public void TerminatingLastGangDoesNotImmediatelyAbandonControlledSector()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)], owner: new PlayerId(0));
        Queue(match, new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Terminate, CommandTarget.None));
        EnterMovement(match);

        match.FinishExecutionPhase();

        Assert.False(match.FindGang(new GangId(10))!.IsActive);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
    }

    [Fact]
    public void MoveRejectsDestinationAlreadyAtFriendlyCapacity()
    {
        var gangs = new List<MatchGangState> { Gang(10, 0, 0, 5) };
        gangs.AddRange(Enumerable.Range(0, MatchLimits.FriendlyGangsPerSector)
            .Select(index => Gang(30 + index, 0, 1, 5)));
        var match = CreateMatch(gangs, [Gang(20, 1, 3, 5)]);
        match.FinishUpkeep();

        var result = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1)));

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.DestinationAtCapacity, result.Validation.Code);
    }

    [Fact]
    public void SimultaneousFriendlyMovesRespectCapacityInQueueOrder()
    {
        var gangs = new List<MatchGangState>
        {
            Gang(10, 0, 0, 5),
            Gang(11, 0, 0, 5)
        };
        gangs.AddRange(Enumerable.Range(0, MatchLimits.FriendlyGangsPerSector - 1)
            .Select(index => Gang(30 + index, 0, 1, 5)));
        var match = CreateMatch(gangs, [Gang(20, 1, 3, 5)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Move, CommandTarget.Sector(1))).Accepted);
        FinishCommands(match);
        EnterMovementFromExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal(1, match.FindGang(new GangId(10))!.SectorId);
        Assert.Equal(0, match.FindGang(new GangId(11))!.SectorId);
        Assert.Equal(
            [CommandResolutionCode.Resolved, CommandResolutionCode.DestinationFull],
            match.LastPhaseResolutions.Select(result => result.Code));
    }

    [Fact]
    public void FriendlyControlCommandsPoolAgainstNeutralSectorIncome()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var match = CreateMatch(
            [Gang(10, 0, 0, 7, controller), Gang(11, 0, 0, 6, controller)],
            [Gang(20, 1, 3, 5)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        Assert.True(match.Submit(Control(0, 11)).Accepted);
        FinishCommands(match);
        EnterControlFromExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(2, match.LastPhaseResolutions.Count);
        var expectedAttack = ManualRules.ControlStrength(
            new[] { match.FindGang(new GangId(10))!, match.FindGang(new GangId(11))! }
                .Select(gang => (gang.Force, EffectiveStatisticsCalculator.ForGang(match, gang).Control)));
        Assert.All(match.LastPhaseResolutions, result =>
        {
            Assert.Equal(expectedAttack, result.Event!.Resolution!.AttackValue);
            Assert.Equal(1, result.Event.Resolution.Successes);
        });
    }

    [Fact]
    public void ControlUsesGeneratedSectorIncomeInsteadOfSiteCashBenefits()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var match = CreateMatch(
            [Gang(10, 0, 0, 10, controller)],
            [Gang(20, 1, 3, 5)],
            income: 7);
        var siteCash = match.Sectors[0].Sites.Sum(site =>
            match.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Cash);
        Assert.NotEqual(siteCash, match.Sectors[0].Income);
        Queue(match, Control(0, 10));
        EnterControl(match);

        match.FinishExecutionPhase();

        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(match.Sectors[0].Income, resolution.DefenseValue);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
    }

    [Fact]
    public void EnemyDefenseCanPreventControl()
    {
        var data = BundledOriginalData.Load();
        var weak = data.Gangs.OrderBy(gang => gang.Stats.Control).First().Id;
        var strong = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var match = CreateMatch(
            [Gang(10, 0, 0, 1, weak)],
            [Gang(20, 1, 0, 10, strong)],
            owner: new PlayerId(1));
        Queue(match, Control(0, 10));
        EnterControl(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(1), match.Sectors[0].Owner);
        Assert.Equal(0, Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.Successes);
    }

    [Fact]
    public void CapturedOwnerReactsTowardNewOwnerByTwiceTheirReaction()
    {
        var data = BundledOriginalData.Load();
        var strong = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var match = CreateMatch(
            [Gang(10, 0, 0, 10, strong)],
            [],
            owner: new PlayerId(1));
        Queue(match, Control(0, 10));
        EnterControl(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(
            Math.Max(AiStrategicState.MinimumAttitude,
                1 - 2 * match.AiStrategy.Reaction(new PlayerId(1))),
            match.AiStrategy.Attitude(new PlayerId(1), new PlayerId(0)));
        Assert.Equal(1, match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
    }

    [Fact]
    public void ZeroMarginControlUsesRecordedDeterministicFiftyPercentChance()
    {
        var first = CreateMatch(
            [Gang(10, 0, 0, 3)], [Gang(20, 1, 3, 5)], filledHirePool: true);
        var second = CreateMatch(
            [Gang(10, 0, 0, 3)], [Gang(20, 1, 3, 5)], filledHirePool: true);
        Queue(first, Control(0, 10));
        Queue(second, Control(0, 10));
        EnterControl(first);
        EnterControl(second);
        var firstConsumptionBefore = first.Random.ConsumptionCount;
        var secondConsumptionBefore = second.Random.ConsumptionCount;
        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        var resolution = Assert.Single(first.LastPhaseResolutions).Event!.Resolution!;
        Assert.Equal(resolution.AttackValue, resolution.DefenseValue);
        Assert.InRange(resolution.ChanceRoll!.Value, 1, 2);
        Assert.Equal(2, resolution.ChanceSides);
        Assert.Equal(resolution.ChanceRoll == 2, first.Sectors[0].Owner == new PlayerId(0));
        Assert.Equal(firstConsumptionBefore + 3, first.Random.ConsumptionCount);
        Assert.Equal(secondConsumptionBefore + 3, second.Random.ConsumptionCount);
        Assert.Equal(resolution.ChanceRoll,
            Assert.Single(second.LastPhaseResolutions).Event!.Resolution!.ChanceRoll);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    [Fact]
    public void UniqueHighestControlMarginWinsNeutralConflictFromPhaseSnapshot()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 3)],
            [Gang(20, 1, 0, 5)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(Control(1, 20)).Accepted);
        match.FinishCommand(new PlayerId(1));
        EnterControlFromExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(1), match.Sectors[0].Owner);
        Assert.Equal(2, match.LastPhaseResolutions.Count);
        Assert.Equal(0, match.LastPhaseResolutions.Single(
            result => result.Command.Player == new PlayerId(0)).Event!.Resolution!.Successes);
        Assert.Equal(1, match.LastPhaseResolutions.Single(
            result => result.Command.Player == new PlayerId(1)).Event!.Resolution!.Successes);
    }

    [Fact]
    public void EqualPositiveControlMarginsRandomlyChooseAPlayerInSlotOrder()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 5)],
            [Gang(20, 1, 0, 5)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(Control(1, 20)).Accepted);
        match.FinishCommand(new PlayerId(1));
        EnterControlFromExecution(match);
        var expectedRandom = new DeterministicRandom(
            match.Random.State, match.Random.ConsumptionCount);
        var expectedRoll = expectedRandom.NextInclusive(2);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(expectedRoll - 1), match.Sectors[0].Owner);
        Assert.Equal(expectedRandom.ConsumptionCount, match.Random.ConsumptionCount);
        Assert.All(match.LastPhaseResolutions, result =>
        {
            var resolution = result.Event!.Resolution!;
            Assert.True(resolution.AttackValue > resolution.DefenseValue);
            Assert.Equal(expectedRoll, resolution.ChanceRoll);
            Assert.Equal(2, resolution.ChanceSides);
        });
        Assert.Single(match.LastPhaseResolutions, result =>
            result.Event!.Resolution!.Successes == 1);
    }

    [Fact]
    public void EqualZeroControlMarginsIncludeNeutralBeforePlayers()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 3)],
            [Gang(20, 1, 0, 3)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(Control(1, 20)).Accepted);
        match.FinishCommand(new PlayerId(1));
        EnterControlFromExecution(match);
        var expectedRandom = new DeterministicRandom(
            match.Random.State, match.Random.ConsumptionCount);
        var expectedRoll = expectedRandom.NextInclusive(3);

        match.FinishExecutionPhase();

        var expectedOwner = expectedRoll == 1 ? (PlayerId?)null : new PlayerId(expectedRoll - 2);
        Assert.Equal(expectedOwner, match.Sectors[0].Owner);
        Assert.Equal(expectedRandom.ConsumptionCount, match.Random.ConsumptionCount);
        Assert.All(match.LastPhaseResolutions, result =>
        {
            var resolution = result.Event!.Resolution!;
            Assert.Equal(resolution.AttackValue, resolution.DefenseValue);
            Assert.Equal(expectedRoll, resolution.ChanceRoll);
            Assert.Equal(3, resolution.ChanceSides);
        });
        Assert.Equal(expectedOwner is null ? 0 : 1,
            match.LastPhaseResolutions.Count(result => result.Event!.Resolution!.Successes == 1));
    }

    [Fact]
    public void NeutralControlConflictHonorsCrackdownStartedAfterSubmission()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 10)],
            [Gang(20, 1, 0, 10)]);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(Control(1, 20)).Accepted);
        match.FinishCommand(new PlayerId(1));
        EnterControlFromExecution(match);
        match.Sectors[0].CrackdownActive = true;

        match.FinishExecutionPhase();

        Assert.Null(match.Sectors[0].Owner);
        Assert.All(match.LastPhaseResolutions, result =>
        {
            Assert.Equal(CommandResolutionCode.SectorInCrackdown, result.Code);
            Assert.Equal(GameEventKind.CommandFailed, result.Event!.Kind);
        });
    }

    [Fact]
    public void ControlledSectorConflictUsesOnePhaseOpeningOwnerAndDefense()
    {
        var match = CreateThreePlayerControlConflict();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(Control(1, 10)).Accepted);
        match.FinishCommand(new PlayerId(1));
        Assert.True(match.Submit(Control(2, 20)).Accepted);
        match.FinishCommand(new PlayerId(2));
        EnterControlFromExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(2), match.Sectors[0].Owner);
        Assert.Equal(0, match.Players[1].Statistics.Overthrows);
        Assert.Equal(1, match.Players[2].Statistics.Overthrows);
        Assert.All(match.LastPhaseResolutions, result =>
            Assert.Equal(0, result.Event!.Resolution!.PreviousValue));
        Assert.Equal(0, match.LastPhaseResolutions.Single(result =>
            result.Command.Player == new PlayerId(1)).Event!.Resolution!.Successes);
        Assert.Equal(1, match.LastPhaseResolutions.Single(result =>
            result.Command.Player == new PlayerId(2)).Event!.Resolution!.Successes);
        Assert.Single(match.LastPhaseResolutions.Select(result =>
            result.Event!.Resolution!.DefenseValue).Distinct());
    }

    [Fact]
    public void ControlRejectsSectorWithActiveCrackdown()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)], crackdownActive: true);
        match.FinishUpkeep();

        var result = match.Submit(Control(0, 10));

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.SectorInCrackdown, result.Validation.Code);
        Assert.Equal("A sector cannot be controlled while police are present.", result.Validation.Message);
    }

    [Fact]
    public void ControlFailsAtExecutionIfCrackdownStartedAfterSubmission()
    {
        var match = CreateMatch(
            [Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)]);
        Queue(match, Control(0, 10));
        EnterControl(match);
        match.Sectors[0].CrackdownActive = true;

        match.FinishExecutionPhase();

        Assert.Null(match.Sectors[0].Owner);
        var result = Assert.Single(match.LastPhaseResolutions);
        Assert.Equal(CommandResolutionCode.SectorInCrackdown, result.Code);
        Assert.Equal(GameEventKind.CommandFailed, result.Event!.Kind);
    }

    [Fact]
    public void HidingDefenderDoesNotResistEnemyControl()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs.First(gang => gang.Stats.Control == -1).Id;
        var defenderDefinition = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var match = CreateMatch(
            [Gang(10, 0, 0, 5, attackerDefinition)],
            [Gang(20, 1, 0, 10, defenderDefinition)],
            owner: new PlayerId(1));
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Hide, CommandTarget.None)).Accepted);
        match.FinishCommand(new PlayerId(1));
        EnterControlFromExecution(match);

        match.FinishExecutionPhase();

        Assert.True(match.FindGang(new GangId(20))!.Hidden);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(match.Sectors[0].Income,
            Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.DefenseValue);
    }

    [Fact]
    public void OverthrowClearsInfluenceAndRestoresBaseResistance()
    {
        var data = BundledOriginalData.Load();
        var strong = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        var weak = data.Gangs.OrderBy(gang => gang.Stats.Control).First().Id;
        var siteSupport = data.Sites.Single(site => site.Id == 0).Support;
        var match = CreateMatch(
            [Gang(10, 0, 0, 10, strong), Gang(11, 0, 0, 10, strong)],
            [Gang(20, 1, 0, 1, weak)],
            owner: new PlayerId(1),
            influencedBy: new PlayerId(1),
            playerOneSupport: siteSupport);
        match.FinishUpkeep();
        Assert.True(match.Submit(Control(0, 10)).Accepted);
        Assert.True(match.Submit(Control(0, 11)).Accepted);
        FinishCommands(match);
        EnterControlFromExecution(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(1, match.Players[0].Statistics.Overthrows);
        Assert.Equal(0, match.Players[1].Support);
        Assert.All(match.Sectors[0].Sites, site =>
        {
            Assert.Null(site.InfluencedBy);
            Assert.Equal(
                match.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Resistance,
                site.Resistance);
        });
    }

    [Fact]
    public void HiddenStateExpiresAtFollowingUpkeep()
    {
        var match = CreateMatch([Gang(10, 0, 0, 5)], [Gang(20, 1, 3, 5)]);
        Queue(match, new GameCommand(new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None));
        FinishCommands(match);

        match.FinishExecutionPhase();
        Assert.True(match.FindGang(new GangId(10))!.Hidden);
        for (var index = 0; index < 5; index++) match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();

        match.FinishUpkeep();

        Assert.False(match.FindGang(new GangId(10))!.Hidden);
    }

    [Fact]
    public void EquivalentControlRunsProduceIdenticalHash()
    {
        var first = CreateMatch([Gang(10, 0, 0, 8)], [Gang(20, 1, 3, 5)]);
        var second = CreateMatch([Gang(10, 0, 0, 8)], [Gang(20, 1, 3, 5)]);
        Queue(first, Control(0, 10));
        Queue(second, Control(0, 10));
        EnterControl(first);
        EnterControl(second);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        Assert.Equal(first.Sectors[0].Owner, second.Sectors[0].Owner);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    [Fact]
    public void CompletedRepeatingControlCommandIsRemovedFromGang()
    {
        var match = CreateMatch([Gang(10, 0, 0, 10)], [Gang(20, 1, 3, 5)]);
        Queue(match, Control(0, 10) with { Repeat = true });
        EnterControl(match);

        match.FinishExecutionPhase();

        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.False(match.Commands.TryGet(new GangId(10), out _));
        Assert.Null(match.FindGang(new GangId(10))!.QueuedCommand);
    }

    private static GameCommand Control(int player, int gang) =>
        new(new PlayerId(player), new GangId(gang), GangAction.Control, CommandTarget.None);

    private static void Queue(MatchState match, GameCommand command)
    {
        match.FinishUpkeep();
        Assert.True(match.Submit(command).Accepted);
    }

    private static void FinishCommands(MatchState match)
    {
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
    }

    private static void EnterMovement(MatchState match)
    {
        FinishCommands(match);
        EnterMovementFromExecution(match);
    }

    private static void EnterMovementFromExecution(MatchState match)
    {
        for (var index = 0; index < 4; index++) match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Movement, match.Coordinator.ExecutionPhase);
    }

    private static void EnterControl(MatchState match)
    {
        FinishCommands(match);
        EnterControlFromExecution(match);
    }

    private static void EnterControlFromExecution(MatchState match)
    {
        for (var index = 0; index < 5; index++) match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Control, match.Coordinator.ExecutionPhase);
    }

    private static MatchGangState Gang(int id, int owner, int sector, int force, short definition = 1) =>
        new(new GangId(id), new PlayerId(owner), definition, sector, force);

    private static MatchState CreateThreePlayerControlConflict()
    {
        var data = BundledOriginalData.Load();
        var weak = data.Gangs.OrderBy(gang => gang.Stats.Control).First().Id;
        var strong = data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "OWNER", PlayerController.Computer),
            new(new PlayerId(1), "FIRST", PlayerController.Computer),
            new(new PlayerId(2), "SECOND", PlayerController.Computer)
        ];
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500, [Gang(30, 0, 0, 1, weak)]),
            new(setups[1], 500, [Gang(10, 1, 0, 5, strong)]),
            new(setups[2], 500, [Gang(20, 2, 0, 10, strong)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : null, income: 2))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }

    private static MatchState CreateMatch(
        IReadOnlyList<MatchGangState> playerZeroGangs,
        IReadOnlyList<MatchGangState> playerOneGangs,
        PlayerId? owner = null,
        PlayerId? influencedBy = null,
        int playerOneSupport = 0,
        bool filledHirePool = false,
        bool crackdownActive = false,
        int income = 2)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500, playerZeroGangs,
                hirePool: filledHirePool ? [1, 2, 3] : null),
            new(setup.Players[1], 500, playerOneGangs, support: playerOneSupport)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, id == 0 && influencedBy is not null ? 0 : 7,
                    id == 0 ? influencedBy : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], id == 0 ? owner : null,
                crackdownActive: id == 0 && crackdownActive,
                income: id == 0 ? income : 2))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}

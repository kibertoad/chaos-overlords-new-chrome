using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class MatchStateTests
{
    [Fact]
    public void CommandRuleTableCoversEveryExecutableAction()
    {
        var executableActions = Enum.GetValues<GangAction>().Where(action => action != GangAction.None);

        Assert.Equal(executableActions.Order(), CommandRules.ByAction.Keys.Order());
    }

    [Fact]
    public void BribeCostIsPartOfTheCommandDescriptor() =>
        Assert.Equal(ManualRules.BribeCost, CommandRules.ByAction[GangAction.Bribe].CashCost);

    [Fact]
    public void MatchRequiresExplicitCompleteBoard()
    {
        var data = BundledOriginalData.Load();
        var setup = Setup();
        var players = Players(setup);

        Assert.Throws<ArgumentException>(() => new MatchState(data, setup, players, Sectors().Take(63).ToArray()));
    }

    [Fact]
    public void RejectedCommandDoesNotMutateQueueOrEventLog()
    {
        var match = CreateMatch();
        var command = new GameCommand(new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1));

        var result = match.Submit(command);

        Assert.False(result.Accepted);
        Assert.Equal(CommandValidationCode.InvalidPhase, result.Validation.Code);
        Assert.Empty(match.Commands.ExecutionPlan());
        Assert.Empty(match.Events);
    }

    [Fact]
    public void QueueReplacementEmitsOrderedEventsAndUpdatesGangProjection()
    {
        var match = CreateMatch();
        match.Coordinator.FinishUpkeep();

        var first = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1)));
        var replacement = match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None, Repeat: true));

        Assert.True(first.Accepted);
        Assert.True(replacement.Accepted);
        Assert.Equal([GameEventKind.CommandQueued, GameEventKind.CommandReplaced], match.Events.Select(item => item.Kind));
        Assert.Equal([0L, 1L], match.Events.Select(item => item.Sequence));
        var gang = match.FindGang(new GangId(10))!;
        Assert.Equal(GangAction.Hide, gang.QueuedCommand!.Command.Action);
        Assert.True(gang.QueuedCommand.Command.Repeat);
    }

    [Fact]
    public void AllActionsHaveExplicitTargetShapes()
    {
        var match = CreateMatch();
        match.Coordinator.FinishUpkeep();
        var player = new PlayerId(0);
        var gang = new GangId(10);

        var commands = new[]
        {
            new GameCommand(player, gang, GangAction.Attack, CommandTarget.Gang(new GangId(20))),
            new GameCommand(player, gang, GangAction.Bribe, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Chaos, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Control, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Equip, CommandTarget.Item(0)),
            new GameCommand(player, gang, GangAction.Give, CommandTarget.Gang(new GangId(11)), SecondaryTarget: CommandTarget.Item(0)),
            new GameCommand(player, gang, GangAction.Heal, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Hide, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Influence, CommandTarget.Site(0)),
            new GameCommand(player, gang, GangAction.Move, CommandTarget.Sector(1)),
            new GameCommand(player, gang, GangAction.Research, CommandTarget.Item(0)),
            new GameCommand(player, gang, GangAction.Sell, CommandTarget.Item(0)),
            new GameCommand(player, gang, GangAction.Snitch, CommandTarget.None),
            new GameCommand(player, gang, GangAction.Terminate, CommandTarget.None)
        };

        Assert.All(commands, command => Assert.NotEqual(
            CommandValidationCode.InvalidTargetKind,
            CommandValidator.Validate(match, command).Code));
    }

    [Fact]
    public void ValidationRejectsOwnershipRelationshipAndSpatialErrors()
    {
        var match = CreateMatch();
        match.Coordinator.FinishUpkeep();
        var player = new PlayerId(0);
        var actor = new GangId(10);

        Assert.Equal(CommandValidationCode.GangNotOwned,
            CommandValidator.Validate(match, new GameCommand(player, new GangId(20), GangAction.Hide, CommandTarget.None)).Code);
        Assert.Equal(CommandValidationCode.TargetNotEnemy,
            CommandValidator.Validate(match, new GameCommand(player, actor, GangAction.Attack, CommandTarget.Gang(new GangId(11)))).Code);
        Assert.Equal(CommandValidationCode.TargetNotFriendly,
            CommandValidator.Validate(match, new GameCommand(player, actor, GangAction.Give, CommandTarget.Gang(new GangId(20)), SecondaryTarget: CommandTarget.Item(0))).Code);
        Assert.True(CommandValidator.Validate(match,
            new GameCommand(player, actor, GangAction.Move, CommandTarget.Sector(9))).IsValid);
        Assert.Equal(CommandValidationCode.DestinationNotAdjacent,
            CommandValidator.Validate(match, new GameCommand(player, actor, GangAction.Move, CommandTarget.Sector(18))).Code);
        Assert.Equal(CommandValidationCode.TargetOutsideSector,
            CommandValidator.Validate(match, new GameCommand(player, actor, GangAction.Influence, CommandTarget.Site(3))).Code);
        Assert.Equal(CommandValidationCode.InvalidTargetKind,
            CommandValidator.Validate(match, new GameCommand(player, actor, GangAction.Give, CommandTarget.Gang(new GangId(11)))).Code);
    }

    [Fact]
    public void CancellationIsValidatedAndRecorded()
    {
        var match = CreateMatch();
        match.Coordinator.FinishUpkeep();
        var player = new PlayerId(0);
        var gang = new GangId(10);
        Assert.True(match.Submit(new GameCommand(player, gang, GangAction.Hide, CommandTarget.None)).Accepted);

        var result = match.Cancel(player, gang);

        Assert.True(result.Accepted);
        Assert.Equal(GameEventKind.CommandCancelled, result.Event!.Kind);
        Assert.Null(match.FindGang(gang)!.QueuedCommand);
        Assert.False(match.Commands.TryGet(gang, out _));
    }

    [Fact]
    public void HealIsRejectedWhenGangAlreadyHasMaximumForce()
    {
        var match = CreateMatch();
        match.Coordinator.FinishUpkeep();
        var gang = match.FindGang(new GangId(10))!;
        gang.Force = ManualRules.MaximumForce;

        var validation = CommandValidator.Validate(match, new GameCommand(
            new PlayerId(0), gang.Id, GangAction.Heal, CommandTarget.None));

        Assert.Equal(CommandValidationCode.GangAtFullForce, validation.Code);
    }

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        var setup = Setup();
        var players = Players(setup);
        players =
        [
            new MatchPlayerState(setup.Players[0], 500,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5),
                new MatchGangState(new GangId(11), new PlayerId(0), 2, 0, 5)
            ]),
            new MatchPlayerState(setup.Players[1], 500,
            [
                new MatchGangState(new GangId(20), new PlayerId(1), 3, 0, 5)
            ])
        ];
        return new MatchState(data, setup, players, Sectors());
    }

    private static MatchSetup Setup()
    {
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        return new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players);
    }

    private static MatchPlayerState[] Players(MatchSetup setup) =>
        setup.Players.Select(player => new MatchPlayerState(player, 500)).ToArray();

    private static MatchSectorState[] Sectors() => Enumerable.Range(0, MatchLimits.SectorCount)
        .Select(id => new MatchSectorState(id,
        [
            new MatchSiteState(0, 0, 7),
            new MatchSiteState(1, 1, 5),
            new MatchSiteState(2, 2, 4)
        ]))
        .ToArray();
}

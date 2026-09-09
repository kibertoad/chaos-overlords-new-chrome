using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AttackTargetRosterTests
{
    [Fact]
    public void TargetCommandsFollowPortraitOwnerOrderBeforeGangOrder()
    {
        var state = CreateMatch();
        var actor = state.Players[0].Gangs[0];
        var commands = new[] { 30, 21, 20 }
            .Select(id => new GameCommand(actor.Owner, actor.Id, GangAction.Attack,
                CommandTarget.Gang(new GangId(id))))
            .ToArray();

        var ordered = AttackTargetRoster.Order(state, commands);

        Assert.Equal([20, 21, 30], ordered.Select(command => command.Target.Id));
        Assert.Equal([1, 1, 2], ordered.Select(command =>
            state.FindGang(new GangId(command.Target.Id))!.Owner.Value));
    }

    [Fact]
    public void TargetCommandsExcludeGangsOutsideTheActorsSector()
    {
        var state = CreateMatch();
        state.FindGang(new GangId(30))!.SectorId = 1;
        var actor = state.FindGang(new GangId(10))!;
        var commands = new[] { 20, 30 }
            .Select(id => new GameCommand(actor.Owner, actor.Id, GangAction.Attack,
                CommandTarget.Gang(new GangId(id))))
            .ToArray();

        var ordered = AttackTargetRoster.Order(state, commands);

        Assert.Equal([20], ordered.Select(command => command.Target.Id));
    }

    [Fact]
    public void TargetCommandsExcludeUndetectedGangs()
    {
        var state = CreateMatch(detectable: false);
        var actor = state.FindGang(new GangId(10))!;
        var command = new GameCommand(actor.Owner, actor.Id, GangAction.Attack,
            CommandTarget.Gang(new GangId(20)));

        Assert.False(state.CanPlayerDetectGang(actor.Owner, new GangId(20)));
        Assert.Empty(AttackTargetRoster.Order(state, [command]));
    }

    private static MatchState CreateMatch(bool detectable = true)
    {
        var data = BundledOriginalData.Load();
        var setupPlayers = Enumerable.Range(0, 3)
            .Select(id => new MatchPlayerSetup(new PlayerId(id), $"P{id + 1}", PlayerController.Human))
            .ToArray();
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setupPlayers);
        var observerDefinition = detectable
            ? data.Gangs.OrderByDescending(gang => gang.Stats.Detect).First()
            : data.Gangs.OrderBy(gang => gang.Stats.Detect).First();
        var targetDefinition = detectable
            ? data.Gangs.OrderBy(gang => gang.Stats.Stealth).First()
            : data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First();
        var gangIds = new[] { new[] { 10 }, new[] { 21, 20 }, new[] { 30 } };
        var players = setupPlayers.Select((player, owner) => new MatchPlayerState(
            player, 20,
            gangIds[owner].Select(id => new MatchGangState(
                new GangId(id), player.Id,
                owner == 0 ? observerDefinition.Id : targetDefinition.Id, 0, 10)).ToArray()))
            .ToArray();
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}

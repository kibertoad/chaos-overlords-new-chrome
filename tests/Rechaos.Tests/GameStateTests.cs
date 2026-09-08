using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class GameStateTests
{
    [Fact]
    public void SameSeedProducesSameCity()
    {
        var data = BundledOriginalData.Load();
        var first = new GameState(data, 42);
        var second = new GameState(data, 42);
        Assert.Equal(first.Sectors.Select(s => s.Summary), second.Sectors.Select(s => s.Summary));
    }

    [Fact]
    public void EndingSixTurnsAdvancesRound()
    {
        var state = new GameState(BundledOriginalData.Load());
        for (var i = 0; i < 6; i++) state.EndTurn();
        Assert.Equal(2, state.Turn);
        Assert.Equal(0, state.CurrentPlayer);
    }

    [Fact]
    public void HireRespectsDocumentedPerSectorCapacity()
    {
        var state = new GameState(BundledOriginalData.Load());
        for (var i = 0; i < GameState.MaximumFriendlyGangsPerSector; i++)
            Assert.True(state.HireGang());

        Assert.False(state.HireGang());
        Assert.Equal(GameState.MaximumFriendlyGangsPerSector, state.Players[0].Gangs.Count);
        Assert.Equal("SECTOR GANG CAPACITY REACHED", state.Message);
    }

    [Fact]
    public void HiredGangsReceiveStableUniqueIdentifiers()
    {
        var state = new GameState(BundledOriginalData.Load());
        Assert.True(state.HireGang());
        Assert.True(state.HireGang());

        Assert.Equal([0, 1], state.Players[0].Gangs.Select(gang => gang.Id.Value));
        Assert.All(state.Players[0].Gangs, gang => Assert.Equal(new PlayerId(0), gang.Owner));
    }

    [Fact]
    public void CommandsCanOnlyBeChangedByOwningCurrentPlayer()
    {
        var state = new GameState(BundledOriginalData.Load());
        Assert.True(state.HireGang());
        var gang = state.Players[0].Gangs.Single();

        Assert.True(state.QueueCommand(gang.Id, GangAction.Move, CommandTarget.Sector(1), repeat: true));
        Assert.True(state.Commands.TryGet(gang.Id, out var command));
        Assert.Equal(GangAction.Move, command!.Command.Action);
        Assert.True(command.Command.Repeat);
        Assert.True(state.CancelCommand(gang.Id));
        Assert.False(state.Commands.TryGet(gang.Id, out _));

        Assert.False(state.QueueCommand(new GangId(999), GangAction.Hide, CommandTarget.None));
        Assert.Equal("GANG IS NOT CONTROLLED BY CURRENT PLAYER", state.Message);
    }
}

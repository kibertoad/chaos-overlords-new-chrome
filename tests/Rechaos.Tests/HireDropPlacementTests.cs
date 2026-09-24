using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireDropPlacementTests
{
    private const int OwnedSector = 9;

    [Fact]
    public void ADropOnASectorWithAFullComplementOfFriendlyGangsIsRefused()
    {
        var match = CreateMatch(MatchLimits.FriendlyGangsPerSector);

        var rejection = HireDropPlacement.Rejection(match, new PlayerId(0), OwnedSector);

        Assert.Equal(HireValidationCode.SectorCapacityReached, rejection?.Code);
        Assert.True(CityStatusMessage.Fits(CityStatusMessage.Error(rejection!.Value.Message)));
    }

    [Fact]
    public void ADropOnASectorWithRoomIsLeftToTheRules()
    {
        var match = CreateMatch(MatchLimits.FriendlyGangsPerSector - 1);

        Assert.Null(HireDropPlacement.Rejection(match, new PlayerId(0), OwnedSector));
    }

    [Fact]
    public void InactiveGangsDoNotTakeUpRoom()
    {
        var match = CreateMatch(MatchLimits.FriendlyGangsPerSector, inactiveGangs: 1);

        Assert.Null(HireDropPlacement.Rejection(match, new PlayerId(0), OwnedSector));
    }

    [Fact]
    public void AGangOrderedToMoveAwayDoesNotTakeUpRoom()
    {
        var match = CreateMatch(MatchLimits.FriendlyGangsPerSector);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(20), GangAction.Move, CommandTarget.Sector(OwnedSector + 1))).Accepted);

        Assert.Null(HireDropPlacement.Rejection(match, new PlayerId(0), OwnedSector));
    }

    [Fact]
    public void AGangOrderedToTerminateDoesNotTakeUpRoom()
    {
        var match = CreateMatch(MatchLimits.FriendlyGangsPerSector);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(20), GangAction.Terminate, CommandTarget.None)).Accepted);

        Assert.Null(HireDropPlacement.Rejection(match, new PlayerId(0), OwnedSector));
    }

    private static MatchState CreateMatch(int gangsInSector, int inactiveGangs = 0)
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var definition = data.Gangs.OrderByDescending(gang => gang.TechLevel).First();
        var gangs = Enumerable.Range(0, gangsInSector)
            .Select(index => new MatchGangState(
                new GangId(20 + index), setupPlayer.Id, definition.Id, OwnedSector, index < inactiveGangs ? 0 : 10))
            .ToArray();
        var player = new MatchPlayerState(setupPlayer, 500, gangs);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ], owner: id == OwnedSector ? setupPlayer.Id : null))
            .ToArray();
        var match = new MatchState(data, setup, [player], sectors);
        match.FinishUpkeep();
        return match;
    }
}

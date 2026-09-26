using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorMapGangDropTests
{
    private const int GangSector = 9;

    [Fact]
    public void DroppingOnTheOccupiedSectorMeansRecurringControl()
    {
        var match = CreateMatch(ownedByPlayer: false);
        var gang = match.Players[0].Gangs[0];

        var intent = SectorMapGangDrop.Intent(gang.SectorId, GangSector);

        Assert.Equal(GangAction.Control, intent.Action);
        Assert.Equal(CommandTarget.None, intent.Target);
        Assert.True(intent.Repeat);
        Assert.True(match.Submit(Command(gang, intent)).Accepted);
    }

    [Fact]
    public void DroppingOnANeighborStillMeansAOneOffMove()
    {
        var match = CreateMatch(ownedByPlayer: false);
        var gang = match.Players[0].Gangs[0];

        var intent = SectorMapGangDrop.Intent(gang.SectorId, GangSector + 1);

        Assert.Equal(GangAction.Move, intent.Action);
        Assert.Equal(CommandTarget.Sector(GangSector + 1), intent.Target);
        Assert.False(intent.Repeat);
        Assert.True(match.Submit(Command(gang, intent)).Accepted);
    }

    [Fact]
    public void DistantAndIllegalDropsAreRefusedByTheRules()
    {
        var match = CreateMatch(ownedByPlayer: false);
        var gang = match.Players[0].Gangs[0];
        var owned = CreateMatch(ownedByPlayer: true);

        Assert.False(match.Submit(
            Command(gang, SectorMapGangDrop.Intent(gang.SectorId, GangSector + 2))).Accepted);
        Assert.False(owned.Submit(Command(owned.Players[0].Gangs[0],
            SectorMapGangDrop.Intent(GangSector, GangSector))).Accepted);
    }

    private static GameCommand Command(MatchGangState gang, BulkCommandIntent intent) =>
        new(gang.Owner, gang.Id, intent.Action, intent.Target, intent.Repeat);

    [Fact]
    public void ControlJoinsTheHighlightedDestinationsOnlyWhileItIsLegal()
    {
        var contested = CreateMatch(ownedByPlayer: false);
        var contestedGang = contested.Players[0].Gangs[0];
        var contestedDestinations = SectorMapGangDrop.Destinations(
            CommandOptionCatalog.LegalCommands(contested, contestedGang.Owner, contestedGang.Id),
            contestedGang.SectorId);

        var owned = CreateMatch(ownedByPlayer: true);
        var ownedGang = owned.Players[0].Gangs[0];
        var ownedDestinations = SectorMapGangDrop.Destinations(
            CommandOptionCatalog.LegalCommands(owned, ownedGang.Owner, ownedGang.Id),
            ownedGang.SectorId);

        Assert.Contains(GangSector, contestedDestinations);
        Assert.Contains(GangSector + 1, contestedDestinations);
        Assert.DoesNotContain(GangSector, ownedDestinations);
        Assert.Contains(GangSector + 1, ownedDestinations);
    }

    [Fact]
    public void RejectedDropsExplainWhyTheSectorCannotBeTaken()
    {
        var match = CreateMatch(ownedByPlayer: true);
        var gang = match.Players[0].Gangs[0];
        var crackdown = CreateMatch(ownedByPlayer: false);
        crackdown.Sectors[GangSector].CrackdownActive = true;
        var full = CreateMatch(ownedByPlayer: false, gangsInNeighbor: MatchLimits.FriendlyGangsPerSector);

        string[] messages =
        [
            SectorMapGangDrop.Rejection(match, gang, GangSector),
            SectorMapGangDrop.Rejection(match, gang, GangSector + 2),
            SectorMapGangDrop.Rejection(crackdown, crackdown.Players[0].Gangs[0], GangSector),
            SectorMapGangDrop.Rejection(full, full.Players[0].Gangs[0], GangSector + 1)
        ];

        Assert.Equal(
            ["SECTOR ALREADY CONTROLLED", "MOVE REQUIRES NEIGHBOR SECTOR",
                "POLICE BLOCK CONTROL ATTEMPT", "SECTOR IS FULL"],
            messages);
        Assert.All(messages, message => Assert.True(CityStatusMessage.Fits(message), message));
    }

    private static MatchState CreateMatch(bool ownedByPlayer, int gangsInNeighbor = 0)
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var definition = data.Gangs.OrderByDescending(gang => gang.TechLevel).First();
        var gang = new MatchGangState(new GangId(10), setupPlayer.Id, definition.Id, GangSector, 10);
        var neighbors = Enumerable.Range(0, gangsInNeighbor)
            .Select(index => new MatchGangState(
                new GangId(20 + index), setupPlayer.Id, definition.Id, GangSector + 1, 10));
        var player = new MatchPlayerState(setupPlayer, 500, [gang, .. neighbors]);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ], owner: ownedByPlayer ? setupPlayer.Id : null))
            .ToArray();
        var match = new MatchState(data, setup, [player], sectors);
        match.FinishUpkeep();
        return match;
    }
}

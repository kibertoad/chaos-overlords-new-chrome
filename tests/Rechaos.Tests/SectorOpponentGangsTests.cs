using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorOpponentGangsTests
{
    [Fact]
    public void OpponentRosterIsListedInCardOrderForTheSelectedSector()
    {
        var state = CreateMatch();
        var viewer = new PlayerId(0);
        var opponent = new PlayerId(1);

        Assert.True(SectorOpponentGangs.Detectable(state, viewer, opponent, sectorId: 0));
        Assert.Equal([20, 21],
            SectorOpponentGangs.InSector(state, viewer, opponent, sectorId: 0)
                .Select(gang => gang.Id.Value));
        // Gang 22 shares the opponent's roster but sits in the next sector along.
        Assert.Equal([10],
            SectorOpponentGangs.InSector(state, viewer, viewer, sectorId: 0)
                .Select(gang => gang.Id.Value));
        Assert.Equal([11],
            SectorOpponentGangs.InSector(state, viewer, viewer, sectorId: 2)
                .Select(gang => gang.Id.Value));
    }

    [Fact]
    public void StealthKeepsAnOpponentRosterOutOfTheWorkspace()
    {
        var state = CreateMatch(detectable: false);
        var viewer = new PlayerId(0);
        var opponent = new PlayerId(1);

        Assert.False(state.CanPlayerDetectGang(viewer, new GangId(20)));
        Assert.False(SectorOpponentGangs.Detectable(state, viewer, opponent, sectorId: 0));
        Assert.Empty(SectorOpponentGangs.InSector(state, viewer, opponent, sectorId: 0));
    }

    [Fact]
    public void DetectionNeverReachesASectorTheViewerIsAbsentFrom()
    {
        var state = CreateMatch();
        var viewer = new PlayerId(0);

        // The viewer holds sectors zero and two, so sector one stays dark even though the same
        // opponent's gangs are detected next door.
        Assert.False(SectorOpponentGangs.Detectable(state, viewer, new PlayerId(1), sectorId: 1));
    }

    [Fact]
    public void ViewerIsNeverTheirOwnOpponentButStillReadsTheirOwnRoster()
    {
        var state = CreateMatch(detectable: false);
        var owner = new PlayerId(1);

        Assert.False(SectorOpponentGangs.Detectable(state, owner, owner, sectorId: 0));
        Assert.Equal([20, 21],
            SectorOpponentGangs.InSector(state, owner, owner, sectorId: 0)
                .Select(gang => gang.Id.Value));
    }

    [Fact]
    public void PortraitHitCellCoversThePortraitAndItsGangBanner()
    {
        Assert.Equal(new Rectangle(16, 4, 32, 32), PlayerPortraitLayout.CityTop(0));
        Assert.Equal(new Rectangle(17, 35, 30, 7), PlayerPortraitLayout.CityGangPresence(0));
        Assert.Equal(new Rectangle(160, 4, 32, 38), PlayerPortraitLayout.CityPortraitHit(2));
        // The banner stops on the Sector workspace's top edge rather than bleeding into it.
        Assert.Equal(SectorDetailLayout.Workspace.Top, PlayerPortraitLayout.CityGangPresence(0).Bottom);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayerPortraitLayout.CityGangPresence(MatchLimits.PlayerCount));
    }

    [Fact]
    public void PortraitHitTestOnlyAnswersForOverlordsInTheMatch()
    {
        var state = CreateMatch();

        Assert.Equal(new PlayerId(0),
            SectorOpponentGangs.PortraitAt(state, PlayerPortraitLayout.CityTop(0).Center));
        Assert.Equal(new PlayerId(2),
            SectorOpponentGangs.PortraitAt(state, PlayerPortraitLayout.CityGangPresence(2).Center));
        Assert.Null(SectorOpponentGangs.PortraitAt(state, PlayerPortraitLayout.CityTop(3).Center));
        Assert.Null(SectorOpponentGangs.PortraitAt(state, SectorDetailLayout.Workspace.Center));
    }

    [Fact]
    public void UnknownOverlordsAndSectorsAreRejected()
    {
        var state = CreateMatch();
        var viewer = new PlayerId(0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SectorOpponentGangs.InSector(state, viewer, new PlayerId(5), sectorId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SectorOpponentGangs.InSector(state, viewer, viewer, MatchLimits.SectorCount));
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
        (int Id, int SectorId)[][] roster =
        [
            [(10, 0), (11, 2)],
            [(21, 0), (20, 0), (22, 1)],
            [(30, 0)]
        ];
        var players = setupPlayers.Select((player, owner) => new MatchPlayerState(
            player, 20,
            roster[owner].Select(entry => new MatchGangState(
                new GangId(entry.Id), player.Id,
                owner == 0 ? observerDefinition.Id : targetDefinition.Id, entry.SectorId, 10)).ToArray()))
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

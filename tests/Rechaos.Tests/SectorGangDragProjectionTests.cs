using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Covers what a held gang drag paints with, and — the point of holding it at all — when it stops
/// describing the board underneath and has to be taken again.
/// </summary>
public sealed class SectorGangDragProjectionTests
{
    private const int GangSector = 9;
    private static readonly GangId Dragged = new(10);
    private static readonly GangId Other = new(11);

    [Fact]
    public void ProjectsTheDestinationsAndTheCardsTheDragPaintsWith()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;

        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        var legalCommands = CommandOptionCatalog.LegalCommands(match, gang.Owner, gang.Id);
        Assert.Equal(legalCommands, projection.LegalCommands);
        Assert.Equal(
            SectorMapGangDrop.Destinations(legalCommands, gang.SectorId),
            projection.LegalSectors);
        Assert.Equal(
            SectorGangView.Visible(match, gang.Owner, GangSector),
            projection.VisibleGangs);
    }

    [Fact]
    public void HoldsWhileTheBoardAndTheSelectedSectorStandStill()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;

        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        Assert.True(projection.Describes(match, Dragged, GangSector));
    }

    [Fact]
    public void RetiresWhenTheMinimapScrollsUnderTheHeldButton()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;

        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        Assert.False(projection.Describes(match, Dragged, GangSector + 1));
    }

    /// <summary>
    /// The hot-seat planning clock expiring under a held button: the same state object advances,
    /// so a projection compared by identity alone would go on painting last turn's destinations.
    /// </summary>
    [Fact]
    public void RetiresWhenTheSameMatchAdvancesInPlace()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;
        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        match.FinishCommand(gang.Owner);

        Assert.False(projection.Describes(match, Dragged, GangSector));
    }

    /// <summary>
    /// An online turn resolving under a held button: the interface is handed a different state,
    /// which a projection compared by turn position alone would mistake for the one it was taken
    /// from.
    /// </summary>
    [Fact]
    public void RetiresWhenTheStateIsReplacedByAnIdenticalOne()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;
        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        var adopted = CreateMatch();

        Assert.Equal(match.Coordinator.Turn, adopted.Coordinator.Turn);
        Assert.Equal(match.Coordinator.Phase, adopted.Coordinator.Phase);
        Assert.False(projection.Describes(adopted, Dragged, GangSector));
    }

    [Fact]
    public void RetiresWhenAnotherGangIsTheOneBeingDragged()
    {
        var match = CreateMatch();
        var gang = match.FindGang(Dragged)!;

        var projection = SectorGangDragProjection.For(match, gang, GangSector);

        Assert.False(projection.Describes(match, Other, GangSector));
    }

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var definition = data.Gangs.OrderByDescending(gang => gang.TechLevel).First();
        MatchGangState[] gangs =
        [
            new(Dragged, setupPlayer.Id, definition.Id, GangSector, 10),
            new(Other, setupPlayer.Id, definition.Id, GangSector, 10)
        ];
        var player = new MatchPlayerState(setupPlayer, 500, gangs);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ]))
            .ToArray();
        var match = new MatchState(data, setup, [player], sectors);
        match.FinishUpkeep();
        return match;
    }
}

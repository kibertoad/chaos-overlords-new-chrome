using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SiteSearchUiTests
{
    [Fact]
    public void ProjectionReturnsSectorsContainingAnySelectedSiteTypeInCityOrder()
    {
        var state = CreateMatch();
        var first = state.Sectors[0].Sites[0].DefinitionId;
        var laterSector = state.Sectors.First(sector => sector.Id > 0
            && sector.Sites.Any(site => site.DefinitionId != first));
        var second = laterSector.Sites.First(site => site.DefinitionId != first).DefinitionId;

        var result = SiteSearchProjection.MatchingSectors(state, new HashSet<short> { first, second });

        Assert.Contains(0, result);
        Assert.Contains(laterSector.Id, result);
        Assert.Equal(result.Order(), result);
        Assert.All(result, sectorId => Assert.Contains(state.Sectors[sectorId].Sites,
            site => site.DefinitionId == first || site.DefinitionId == second));
    }

    [Fact]
    public void EmptySelectionProducesNoHighlightedSectors()
    {
        var state = CreateMatch();

        Assert.Empty(SiteSearchProjection.MatchingSectors(state, new HashSet<short>()));
    }

    private static MatchState CreateMatch() => OriginalMatchFactory.Create(
        BundledOriginalData.Load(),
        new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "PLAYER 1", PlayerController.Human, 0)
        ], allowSparsePlayerIds: true));
}

using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CityConsoleLayoutTests
{
    [Theory]
    [InlineData(500, 126, CityConsoleAction.Events)]
    [InlineData(552, 126, CityConsoleAction.ComlinkView)]
    [InlineData(599, 158, CityConsoleAction.ComlinkView)]
    [InlineData(552, 159, CityConsoleAction.ComlinkSend)]
    [InlineData(547, 210, CityConsoleAction.CombatSummary)]
    [InlineData(500, 211, CityConsoleAction.CombatDetail)]
    [InlineData(599, 210, CityConsoleAction.FinanceCity)]
    [InlineData(552, 211, CityConsoleAction.FinanceSector)]
    [InlineData(547, 262, CityConsoleAction.Gangs)]
    [InlineData(500, 263, CityConsoleAction.Hire)]
    [InlineData(599, 254, CityConsoleAction.Ranking)]
    [InlineData(552, 255, CityConsoleAction.Search)]
    [InlineData(500, 282, CityConsoleAction.Done)]
    [InlineData(588, 41, CityConsoleAction.GameInfo)]
    public void RoutesEveryNativeSubcontrolFromThePressPoint(
        int x,
        int y,
        CityConsoleAction expected) =>
        Assert.Equal(expected, CityConsoleLayout.ActionAt(new Point(x, y)));

    [Fact]
    public void UsesNativeTilesVerticalSubcontrolsAndPressedSources()
    {
        Assert.Equal(new Rectangle(500, 126, 48, 48), CityConsoleLayout.Events);
        Assert.Equal(new Rectangle(552, 126, 48, 48), CityConsoleLayout.Comlink);
        Assert.Equal(new Rectangle(500, 178, 48, 48), CityConsoleLayout.Combat);
        Assert.Equal(new Rectangle(552, 178, 48, 48), CityConsoleLayout.Finance);
        Assert.Equal(new Rectangle(500, 230, 48, 48), CityConsoleLayout.GangHire);
        Assert.Equal(new Rectangle(552, 230, 48, 48), CityConsoleLayout.RankingSearch);
        Assert.Equal(new Rectangle(500, 282, 100, 48), CityConsoleLayout.Done);
        Assert.Equal(new Rectangle(588, 41, 26, 34), CityConsoleLayout.GameInfo);

        Assert.Equal(new Rectangle(500, 178, 48, 33), CityConsoleLayout.CombatSummary);
        Assert.Equal(new Rectangle(500, 211, 48, 15), CityConsoleLayout.CombatDetail);
        Assert.Equal(CityConsoleAction.CombatSummary,
            CityConsoleLayout.ActionAt(new Point(520, 210)));
        Assert.Equal(CityConsoleAction.CombatDetail,
            CityConsoleLayout.ActionAt(new Point(520, 211)));
        Assert.Equal(CityConsoleAction.Ranking,
            CityConsoleLayout.ActionAt(new Point(576, 254)));
        Assert.Equal(CityConsoleAction.Search,
            CityConsoleLayout.ActionAt(new Point(576, 255)));
        Assert.Equal(CityConsoleControl.Combat,
            CityConsoleLayout.HitTest(new Point(547, 225)));
        Assert.Null(CityConsoleLayout.HitTest(new Point(548, 225)));

        Assert.Equal(new Rectangle(96, 512, 48, 48),
            CityConsoleLayout.PressedSource(CityConsoleControl.Combat));
        Assert.Equal(new Rectangle(288, 512, 100, 48),
            CityConsoleLayout.PressedSource(CityConsoleControl.Done));
        Assert.Equal(new Rectangle(190, 386, 26, 34),
            CityConsoleLayout.PressedSource(CityConsoleControl.GameInfo));
    }
}

using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CityConsoleLayoutTests
{
    [Theory]
    [InlineData(500, 126, CityConsoleAction.Events)]
    [InlineData(552, 126, CityConsoleAction.ComlinkView)]
    [InlineData(584, 173, CityConsoleAction.ComlinkView)]
    [InlineData(585, 126, CityConsoleAction.ComlinkSend)]
    [InlineData(532, 178, CityConsoleAction.CombatSummary)]
    [InlineData(533, 225, CityConsoleAction.CombatDetail)]
    [InlineData(584, 178, CityConsoleAction.FinanceCity)]
    [InlineData(585, 225, CityConsoleAction.FinanceSector)]
    [InlineData(532, 230, CityConsoleAction.Gangs)]
    [InlineData(533, 277, CityConsoleAction.Hire)]
    [InlineData(576, 230, CityConsoleAction.Ranking)]
    [InlineData(577, 277, CityConsoleAction.Search)]
    [InlineData(500, 282, CityConsoleAction.Done)]
    [InlineData(588, 41, CityConsoleAction.GameInfo)]
    public void RoutesEveryNativeSubcontrolFromThePressPoint(
        int x,
        int y,
        CityConsoleAction expected) =>
        Assert.Equal(expected, CityConsoleLayout.ActionAt(new Point(x, y)));

    [Fact]
    public void UsesNativeTilesHorizontalSubcontrolsAndPressedSources()
    {
        Assert.Equal(new Rectangle(500, 126, 48, 48), CityConsoleLayout.Events);
        Assert.Equal(new Rectangle(552, 126, 48, 48), CityConsoleLayout.Comlink);
        Assert.Equal(new Rectangle(500, 178, 48, 48), CityConsoleLayout.Combat);
        Assert.Equal(new Rectangle(552, 178, 48, 48), CityConsoleLayout.Finance);
        Assert.Equal(new Rectangle(500, 230, 48, 48), CityConsoleLayout.GangHire);
        Assert.Equal(new Rectangle(552, 230, 48, 48), CityConsoleLayout.RankingSearch);
        Assert.Equal(new Rectangle(500, 282, 100, 48), CityConsoleLayout.Done);
        Assert.Equal(new Rectangle(588, 41, 26, 34), CityConsoleLayout.GameInfo);

        Assert.Equal(new Rectangle(500, 178, 33, 48), CityConsoleLayout.CombatSummary);
        Assert.Equal(new Rectangle(533, 178, 15, 48), CityConsoleLayout.CombatDetail);
        Assert.Equal(CityConsoleAction.CombatSummary,
            CityConsoleLayout.ActionAt(new Point(532, 200)));
        Assert.Equal(CityConsoleAction.CombatDetail,
            CityConsoleLayout.ActionAt(new Point(533, 200)));
        Assert.Equal(CityConsoleAction.Ranking,
            CityConsoleLayout.ActionAt(new Point(576, 250)));
        Assert.Equal(CityConsoleAction.Search,
            CityConsoleLayout.ActionAt(new Point(577, 250)));
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

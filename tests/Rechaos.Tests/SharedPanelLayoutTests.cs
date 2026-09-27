using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SharedPanelLayoutTests
{
    [Fact]
    public void DescendantsUseOneNativeLocalCoordinateSystem()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), SharedPanelLayout.Panel);
        Assert.Equal(104, SharedPanelLayout.X(0));
        Assert.Equal(124, SharedPanelLayout.Y(0));
        Assert.Equal(new Rectangle(130, 141, 64, 64),
            SharedPanelLayout.At(26, 17, 64, 64));

        Rectangle[] panels =
        [
            EquipmentCommandLayout.Panel, GangInformationLayout.Panel,
            ComlinkViewLayout.Panel, ComlinkSendLayout.Panel,
            CombatPanelLayout.Panel, CombatResultsLayout.Panel,
            LastTurnEventsLayout.Panel, MovementLayout.Panel, PlayerRankingLayout.Panel,
            SectorGangsLayout.Panel, SiteSearchLayout.Panel
        ];
        Assert.All(panels, panel => Assert.Equal(SharedPanelLayout.Panel, panel));
        Assert.Equal(new Rectangle(128, 124, 320, 209), FinanceLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), SiteInformationLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), HireComparisonLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), GameInformationLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), GangDefinitionInformationLayout.Panel);
        Assert.Equal(new Rectangle(128, 124, 320, 209), ItemInformationLayout.Panel);
        Assert.Equal(SharedPanelLayout.Y(119), GangInformationLayout.StatisticY(0));
        Assert.Equal(244, SiteInformationLayout.StatisticY(0));
        Assert.Equal(SharedPanelLayout.At(34, 13, 12, 7), CombatResultsLayout.PageNumber);
        Assert.Equal(SharedPanelLayout.At(70, 13, 12, 7), CombatResultsLayout.PageCount);
        Assert.Equal(CombatResultsLayout.PageNumber, LastTurnEventsLayout.PageNumber);
        Assert.Equal(CombatResultsLayout.PageCount, LastTurnEventsLayout.PageCount);
    }
}

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
            SiteInformationLayout.Panel, ItemInformationLayout.Panel,
            ComlinkViewLayout.Panel, ComlinkSendLayout.Panel,
            CombatPanelLayout.Panel, CombatResultsLayout.Panel,
            LastTurnEventsLayout.Panel, FinanceLayout.Panel,
            GameInformationLayout.Panel, HireComparisonLayout.Panel,
            MovementLayout.Panel, PlayerRankingLayout.Panel,
            SectorGangsLayout.Panel, SiteSearchLayout.Panel
        ];
        Assert.All(panels, panel => Assert.Equal(SharedPanelLayout.Panel, panel));
        Assert.Equal(SharedPanelLayout.Y(119), GangInformationLayout.StatisticY(0));
        Assert.Equal(SharedPanelLayout.Y(120), SiteInformationLayout.StatisticY(0));
        Assert.Equal(SharedPanelLayout.At(29, 11, 58, 12), CombatResultsLayout.Page);
        Assert.Equal(SharedPanelLayout.At(34, 13, 47, 7), LastTurnEventsLayout.Page);
    }
}

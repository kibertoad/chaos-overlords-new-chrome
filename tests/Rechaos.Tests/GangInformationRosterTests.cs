using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangInformationRosterTests
{
    [Fact]
    public void SectorRosterKeepsOnlyActiveLocalEntriesInStableIdOrder()
    {
        var player = new PlayerId(0);
        MatchGangState[] gangs =
        [
            new(new GangId(8), player, 2, 11, 7),
            new(new GangId(3), player, 1, 11, 6),
            new(new GangId(1), player, 0, 10, 8),
            new(new GangId(5), player, 3, 11, 0)
        ];

        Assert.Equal([3, 8], GangInformationRoster.ForSector(gangs, 11)
            .Select(gang => gang.Id.Value));
        Assert.Empty(GangInformationRoster.ForSector(gangs, 12));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GangInformationRoster.ForSector(gangs, MatchLimits.SectorCount));
    }

    [Fact]
    public void LiveGangEquipmentUsesThreeOriginalInformationCells()
    {
        Assert.Equal(new Rectangle(394, 146, 40, 40), GangInformationLayout.Equipment(0));
        Assert.Equal(new Rectangle(394, 210, 40, 40), GangInformationLayout.Equipment(1));
        Assert.Equal(new Rectangle(394, 274, 40, 40), GangInformationLayout.Equipment(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangInformationLayout.Equipment(3));
    }
}

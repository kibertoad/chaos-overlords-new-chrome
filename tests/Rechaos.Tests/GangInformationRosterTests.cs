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

    /// <summary>Arrows on another player's gang panel stay on that player's visible gangs.</summary>
    [Fact]
    public void OwnerRosterKeepsOnlyThatPlayersGangsInTheSector()
    {
        var viewer = new PlayerId(0);
        var rival = new PlayerId(2);
        MatchGangState[] visible =
        [
            new(new GangId(4), viewer, 0, 11, 7),
            new(new GangId(9), rival, 1, 11, 6),
            new(new GangId(6), rival, 2, 11, 5),
            new(new GangId(7), rival, 3, 12, 5)
        ];

        Assert.Equal([6, 9], GangInformationRoster.ForOwnerInSector(visible, rival, 11)
            .Select(gang => gang.Id.Value));
        Assert.Equal([4], GangInformationRoster.ForOwnerInSector(visible, viewer, 11)
            .Select(gang => gang.Id.Value));
    }

    [Fact]
    public void LiveGangEquipmentUsesThreeOriginalInformationCells()
    {
        Assert.Equal(new Rectangle(394, 145, 40, 40), GangInformationLayout.Equipment(0));
        Assert.Equal(new Rectangle(394, 209, 40, 40), GangInformationLayout.Equipment(1));
        Assert.Equal(new Rectangle(394, 273, 40, 40), GangInformationLayout.Equipment(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangInformationLayout.Equipment(3));
    }

    [Fact]
    public void GangValueFieldsCoverOnlyTheTwoLiveGlyphCells()
    {
        Assert.Equal(new Rectangle(276, 243, 12, 7),
            GangInformationLayout.ValueField(GangInformationLayout.LeftValueLeft, 243));
        Assert.Equal(282, GangInformationLayout.ValueTextLeft(
            GangInformationLayout.LeftValueLeft, "3"));
        Assert.Equal(276, GangInformationLayout.ValueTextLeft(
            GangInformationLayout.LeftValueLeft, "10"));
    }
}

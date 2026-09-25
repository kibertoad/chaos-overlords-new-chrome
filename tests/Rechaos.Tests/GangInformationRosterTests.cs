using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangInformationRosterTests
{
    // RULE-UI-010, SCR-UI-005: the columns follow the roster slots, the order of the gang list.
    [Fact]
    public void SectorRosterKeepsOnlyActiveLocalEntriesInRosterSlotOrder()
    {
        var player = new PlayerId(0);
        MatchGangState[] gangs =
        [
            new(new GangId(8), player, 2, 11, 7),
            new(new GangId(3), player, 1, 11, 6),
            new(new GangId(1), player, 0, 10, 8),
            new(new GangId(5), player, 3, 11, 0)
        ];

        // RULE-UI-010 sector_roster_slots: roster slot order, whatever the gang ids.
        Assert.Equal([8, 3], GangInformationRoster.ForSector(gangs, 11)
            .Select(gang => gang.Id.Value));
        Assert.Empty(GangInformationRoster.ForSector(gangs, 12));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GangInformationRoster.ForSector(gangs, MatchLimits.SectorCount));
    }

    // SCR-GANG-002, FND-GANG-006: 48-by-48 rotation frames at (392, 141 + 64k), double-clicked
    // on (391, 140 + 64k, 50, 50).
    [Fact]
    public void LiveGangEquipmentUsesThreeOriginalInformationCells()
    {
        Assert.Equal(new Rectangle(392, 141, 48, 48), GangInformationLayout.Equipment(0));
        Assert.Equal(new Rectangle(392, 205, 48, 48), GangInformationLayout.Equipment(1));
        Assert.Equal(new Rectangle(392, 269, 48, 48), GangInformationLayout.Equipment(2));
        Assert.Equal(new Rectangle(391, 140, 50, 50), GangInformationLayout.EquipmentHit(0));
        Assert.Equal(new Rectangle(391, 268, 50, 50), GangInformationLayout.EquipmentHit(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangInformationLayout.Equipment(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangInformationLayout.EquipmentHit(3));
    }

    // SCR-GANG-002, FND-GANG-006: the close face, the text and the value rows.
    [Fact]
    public void GangInformationPanelFollowsTheRecordedPositions()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), GangInformationLayout.Panel);
        Assert.Equal(new Rectangle(130, 141, 64, 64), GangInformationLayout.Portrait);
        Assert.Equal(new Rectangle(137, 293, 49, 22), GangInformationLayout.Ok);
        Assert.Equal(204, GangInformationLayout.NameLeft);
        Assert.Equal(151, GangInformationLayout.NameY);
        Assert.Equal([169, 178, 187], Enumerable.Range(0, 3).Select(GangInformationLayout.DescriptionY));
        Assert.Equal(276, GangInformationLayout.LeftValueLeft);
        Assert.Equal(372, GangInformationLayout.RightValueLeft);
        Assert.Equal(216, GangInformationLayout.ForceY);
        Assert.Equal(225, GangInformationLayout.TechLevelY);
        Assert.Equal([243, 252, 270, 279, 288, 297, 306],
            Enumerable.Range(0, 7).Select(GangInformationLayout.StatisticY));
        // The base values start at x 258 and x 354.
        Assert.Equal(258, GangInformationLayout.LeftValueLeft - GangInformationLayout.BaseValueOffset);
        Assert.Equal(354, GangInformationLayout.RightValueLeft - GangInformationLayout.BaseValueOffset);
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

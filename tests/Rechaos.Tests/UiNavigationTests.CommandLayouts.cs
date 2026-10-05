using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class UiNavigationTests
{
    [Fact]
    public void CommandOverlayUsesOriginalActionFirstOrdering()
    {
        Assert.Equal(
        [
            GangAction.Attack, GangAction.Bribe, GangAction.Chaos, GangAction.Control,
            GangAction.Equip, GangAction.Give, GangAction.Heal, GangAction.Hide,
            GangAction.Influence, GangAction.Move, GangAction.Research, GangAction.Sell,
            GangAction.Snitch, GangAction.None, GangAction.Terminate
        ], CommandOverlayLayout.Actions);
        Assert.True(CommandOverlayLayout.OpensTargetPicker(GangAction.Equip));
        Assert.True(CommandOverlayLayout.OpensTargetPicker(GangAction.Move));
        Assert.False(CommandOverlayLayout.OpensTargetPicker(GangAction.Chaos));
        Assert.Equal(new Rectangle(256, 70, 158, 22), CommandOverlayLayout.ActionRow(0));
        Assert.Equal(new Rectangle(104, 124, 344, 209), EquipmentCommandLayout.Panel);
        Assert.Equal(new Rectangle(251, 149, 181, 9), EquipmentCommandLayout.ItemRow(0));
        // SCR-EQUIP-001: the frame lies one pixel outside category cell n.
        Assert.Equal(new Rectangle(207, 139, 34, 34), EquipmentCommandLayout.Category(0));
        Assert.Equal(new Rectangle(207, 247, 34, 34), EquipmentCommandLayout.Category(3));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(0));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(1));
        Assert.Equal(1, EquipmentCommandLayout.CategoryForItemType(2));
        Assert.Equal(2, EquipmentCommandLayout.CategoryForItemType(3));
        Assert.Equal(3, EquipmentCommandLayout.CategoryForItemType(4));
        Assert.Equal(new Rectangle(130, 141, 64, 64), GangInformationLayout.Portrait);
        Assert.Equal(new Rectangle(259, 88, 64, 9), SectorGangCardLayout.AssignedCommand(0));
        Assert.Equal(new Rectangle(156, 139, 120, 64), SiteInformationLayout.Portrait);
        Assert.Equal(169, SiteInformationLayout.DataY(0));
        Assert.Equal(187, SiteInformationLayout.DataY(1));
        Assert.Equal(244, SiteInformationLayout.StatisticY(0));
        Assert.Equal(271, SiteInformationLayout.StatisticY(2));
        Assert.Equal(307, SiteInformationLayout.StatisticY(6));
        Assert.Equal(new Rectangle(162, 141, 48, 48), ItemInformationLayout.Portrait);
        Assert.Equal(new Rectangle(176, 155, 20, 20), ItemInformationLayout.CompactPortrait);
        Assert.Equal(25, ItemInformationLayout.TypeStringBase);
        Assert.Equal(243, ItemInformationLayout.StatisticY(0));
        Assert.Equal(270, ItemInformationLayout.StatisticY(2));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatPanelLayout.Panel);
        Assert.Equal(new Rectangle(135, 135, 54, 52), CombatPanelLayout.Sector);
        Assert.Equal(new Point(156, 190), CombatPanelLayout.SectorCodeText);
        Assert.Equal(new Rectangle(137, 293, 50, 23), CombatPanelLayout.Exit);
        Assert.Equal(new Rectangle(253, 254, 67, 64), CombatPanelLayout.LeftAction);
        Assert.Equal(new Rectangle(324, 254, 67, 64), CombatPanelLayout.RightAction);
        Assert.Equal(new Rectangle(254, 172, 64, 64), CombatPanelLayout.GangPortrait(false));
        Assert.Equal(new Rectangle(327, 172, 64, 64), CombatPanelLayout.GangPortrait(true));
        Assert.Equal(new Rectangle(254, 254, 64, 64), CombatPanelLayout.Animation(false));
        Assert.Equal(new Rectangle(327, 254, 64, 64), CombatPanelLayout.Animation(true));
        // FND-COMBAT-014: the header strips and the police areas of PX00300.
        Assert.Equal(new Rectangle(205, 137, 18, 32), CombatPanelLayout.HeaderColor(false));
        Assert.Equal(new Rectangle(328, 137, 18, 32), CombatPanelLayout.HeaderColor(true));
        Assert.Equal(new Rectangle(223, 137, 32, 32), CombatPanelLayout.HeaderPortrait(false));
        Assert.Equal(new Rectangle(346, 137, 32, 32), CombatPanelLayout.HeaderPortrait(true));
        Assert.Equal(new Point(257, 138), CombatPanelLayout.HeaderName(false));
        Assert.Equal(new Point(380, 138), CombatPanelLayout.HeaderName(true));
        Assert.Equal(new Rectangle(257, 138, 60, 8), CombatPanelLayout.HeaderClear(false));
        Assert.Equal(new Rectangle(326, 135, 116, 36), CombatPanelLayout.HeaderClear(true));
        Assert.Equal(new Rectangle(326, 135, 116, 36), CombatPanelLayout.PoliceHeader);
        Assert.Equal(new Rectangle(208, 0, 116, 36), CombatPanelLayout.PoliceHeaderSource);
        Assert.Equal(new Rectangle(0, 0, 64, 64), CombatPanelLayout.PolicePortraitSource);
        Assert.Equal(new Rectangle(64, 0, 48, 48), CombatPanelLayout.PoliceItemSource(0));
        Assert.Equal(new Rectangle(112, 0, 48, 48), CombatPanelLayout.PoliceItemSource(1));
        Assert.Equal(new Rectangle(160, 0, 48, 48), CombatPanelLayout.PoliceItemSource(2));
        Assert.Equal(new Rectangle(393, 172, 48, 48), CombatPanelLayout.EquipmentItem(true, 0));
        Assert.Equal(new Rectangle(393, 270, 48, 48), CombatPanelLayout.EquipmentItem(true, 2));
        Assert.Equal(12, CombatAnimationRouting.DimmedFramesTick);
        Assert.Equal(new Rectangle(256, 240, 60, 3), CombatPanelLayout.ForceBar(false, 0));
        Assert.Equal(new Rectangle(256, 247, 60, 3), CombatPanelLayout.ForceBar(false, 1));
        Assert.Equal(new Rectangle(329, 240, 60, 3), CombatPanelLayout.ForceBar(true, 0));
        Assert.Equal(new Rectangle(329, 247, 60, 3), CombatPanelLayout.ForceBar(true, 1));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatResultsLayout.Panel);
        Assert.Equal(new Rectangle(135, 191, 54, 52), CombatResultsLayout.Sector);
        Assert.Equal(new Rectangle(202, 140, 94, 179), CombatResultsLayout.FriendlyPanel);
        Assert.Equal(new Rectangle(207, 153, 40, 40), CombatResultsLayout.Force(0, enemy: false));
        Assert.Equal(new Rectangle(394, 257, 40, 40), CombatResultsLayout.Force(5, enemy: true));
        Assert.Equal(new Rectangle(306, 284, 32, 32), CombatResultsLayout.Opponent(4));
        Assert.Equal(new Point(156, 246), CombatResultsLayout.SectorCodeText);
        Assert.Equal(EquipmentCommandLayout.Panel, LastTurnEventsLayout.Panel);
        // SCR-EVENT-001; LastTurnEventsLayoutTests pins every element of the panel.
        Assert.Equal(new Rectangle(198, 135, 242, 158), LastTurnEventsLayout.Artwork);
        Assert.Equal(new Rectangle(135, 157, 26, 23), LastTurnEventsLayout.Previous);
    }
}

using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EquipmentGiveUiTests
{
    [Fact]
    public void LayoutMatchesOriginalEquipmentToGivePanel()
    {
        // SCR-GIVE-001 drawn elements and mouse input.
        Assert.Equal(new Rectangle(104, 124, 344, 209), EquipmentGiveLayout.Panel);
        Assert.Equal(new Rectangle(130, 141, 64, 64), EquipmentGiveLayout.Portrait);
        Assert.Equal(new Rectangle(209, 141, 48, 48), EquipmentGiveLayout.ItemPicture(0));
        Assert.Equal(new Rectangle(209, 269, 48, 48), EquipmentGiveLayout.ItemPicture(2));
        Assert.Equal(new Rectangle(206, 138, 54, 54), EquipmentGiveLayout.ItemFrame(0));
        Assert.Equal(new Rectangle(206, 266, 54, 54), EquipmentGiveLayout.ItemFrame(2));
        Assert.Equal(new Rectangle(414, 13, 54, 54), EquipmentGiveLayout.ItemFrameSource);
        Assert.Equal(new Rectangle(137, 261, 49, 22), EquipmentGiveLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), EquipmentGiveLayout.Ok);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentGiveLayout.ItemPicture(3));
    }

    [Fact]
    public void RecipientCardsFollowTheOriginalList()
    {
        // SCR-GIVE-001, FND-GIVE-001, FND-GIVE-002: the card is also the selection target.
        Assert.Equal(new Rectangle(312, 139, 97, 34), EquipmentGiveLayout.RecipientHit(0));
        Assert.Equal(new Rectangle(312, 283, 97, 34), EquipmentGiveLayout.RecipientHit(4));
        Assert.Equal(new Rectangle(0, 560, 97, 34), EquipmentGiveLayout.RecipientCardSource);
        Assert.Equal(new Rectangle(313, 140, 32, 32), EquipmentGiveLayout.RecipientPortrait(0));
        Assert.Equal(new Rectangle(313, 284, 32, 32), EquipmentGiveLayout.RecipientPortrait(4));
        Assert.Equal(new Rectangle(347, 179, 42, 3), EquipmentGiveLayout.RecipientForce(1, 7));
        Assert.Equal(new Rectangle(354, 0, 42, 3), EquipmentGiveLayout.RecipientForceSource(7));
        Assert.Equal(new Rectangle(346, 150, 20, 20), EquipmentGiveLayout.RecipientItem(0, 0));
        Assert.Equal(new Rectangle(367, 186, 20, 20), EquipmentGiveLayout.RecipientItem(1, 1));
        Assert.Equal(new Rectangle(388, 294, 20, 20), EquipmentGiveLayout.RecipientItem(4, 2));
        Assert.Equal(new Rectangle(274, 140, 32, 32), EquipmentGiveLayout.RecipientMarker(0));
        Assert.Equal(new Rectangle(274, 284, 32, 32), EquipmentGiveLayout.RecipientMarker(4));
        Assert.Equal(new Rectangle(128, 448, 32, 32), EquipmentGiveLayout.RecipientMarkerSource);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentGiveLayout.RecipientHit(5));
    }

    [Fact]
    public void RecipientsAreTheOtherLocalGangsInRosterOrderUpToFive()
    {
        var player = new PlayerId(0);
        var giver = new MatchGangState(new GangId(4), player, 0, 11, 6);
        MatchGangState[] gangs =
        [
            new(new GangId(9), player, 1, 11, 6),
            giver,
            new(new GangId(2), player, 1, 11, 6),
            new(new GangId(3), player, 1, 12, 6),
            new(new GangId(5), player, 1, 11, 6),
            new(new GangId(6), player, 1, 11, 6),
            new(new GangId(7), player, 1, 11, 6),
            new(new GangId(8), player, 1, 11, 6)
        ];

        Assert.Equal([2, 5, 6, 7, 8],
            EquipmentGiveSelection.Recipients(gangs, giver).Select(gang => gang.Value));
    }

    [Fact]
    public void RecipientTechLevelMustReachTheHighestSelectedItem()
    {
        // FND-GIVE-001: the requirement starts from 0 and equal values qualify.
        var items = BundledOriginalData.Load().Items;
        var low = items.OrderBy(item => item.TechLevel).First();
        var high = items.OrderByDescending(item => item.TechLevel).First();

        Assert.Equal(0, EquipmentGiveSelection.RequiredTechLevel([]));
        Assert.Equal(high.TechLevel, EquipmentGiveSelection.RequiredTechLevel([low, high]));
        Assert.True(EquipmentGiveSelection.CanReceive(high.TechLevel, high.TechLevel));
        Assert.False(EquipmentGiveSelection.CanReceive(high.TechLevel - 1, high.TechLevel));
    }

    [Fact]
    public void ExistingGiveOrderRestoresItsItemsAndRecipient()
    {
        var queued = EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(1), new GangId(7), [(short)40, (short)12]);

        var (items, recipient) = EquipmentGiveSelection.OpeningSelection(queued, [12, null, 40]);
        Assert.Equal([true, false, true], items);
        Assert.Equal(new GangId(7), recipient);

        var nothing = EquipmentGiveSelection.OpeningSelection(null, [12, null, 40]);
        Assert.Equal([false, false, false], nothing.Items);
        Assert.Null(nothing.Recipient);
    }

    [Fact]
    public void SelectionBuildsOneRecipientAndThreeExactItemTargets()
    {
        var command = EquipmentGiveSelection.CreateCommand(
            new PlayerId(2), new GangId(17), new GangId(18),
            [(short)4, (short)31, (short)44], repeat: true);

        Assert.Equal(GangAction.Give, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(18)), command.Target);
        Assert.Equal(CommandTarget.Item(4), command.SecondaryTarget);
        Assert.Equal(CommandTarget.Item(31), command.TertiaryTarget);
        Assert.Equal(CommandTarget.Item(44), command.QuaternaryTarget);
        Assert.True(command.Repeat);
    }

    [Fact]
    public void SelectionRequiresOneToThreeDistinctItems()
    {
        Assert.Throws<ArgumentException>(() => EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), []));
        Assert.Throws<ArgumentException>(() => EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), [(short)0, 1, 2, 3]));
        var deduplicated = EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), [(short)4, 4]);
        Assert.Null(deduplicated.TertiaryTarget);
    }

    /// <summary>
    /// SCR-GIVE-001, FND-GIVE-003: an ineligible card is covered with black through bitmap 146,
    /// the pattern 48,000 selects, from the card's corner: a quarter of its pixels keep the card.
    /// </summary>
    [Fact]
    public void IneligibleCardsAreDimmedThroughTheSparsePatternFromTheCardsCorner()
    {
        Assert.Equal(OriginalPatternMask.Sparse, EquipmentGiveLayout.IneligibleCardPattern);
        var card = EquipmentGiveLayout.RecipientHit(0);
        var pixels = OriginalPatternMask.ShadedRectangle(
            EquipmentGiveLayout.IneligibleCardPattern, card.Width, card.Height, Color.Black, Color.Black);
        Assert.Equal(Color.Transparent, pixels[0]);
        Assert.Equal(Color.Black, pixels[1]);
        Assert.Equal(Color.Transparent, pixels[card.Width + 2]);
        Assert.Equal(Color.Black, pixels[card.Width]);
    }
}

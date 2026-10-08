using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    // FND-UI-052: the rotating item starts from frame 0 when the panel opens.
    private TimeSpan _itemDetailsOpenedAt;

    private void OpenItemDetails(short itemId, ClientScreen returnScreen = ClientScreen.Commands)
    {
        _itemDetailsId = itemId;
        _itemDetailsOpenedAt = PresentationDrawTime;
        _itemDetailsReturnScreen = returnScreen;
        // FND-UI-053, FND-SELL-001, FND-GIVE-001: Sell and Give call Item Information from their
        // own loop, so their frame local does not step while it runs.
        if (returnScreen is ClientScreen.Sell or ClientScreen.Give)
            _equipmentRotationHeld = _eventPump.Ticks - _equipmentRotationStart;
        _screens.Show(ClientScreen.ItemInformation);
    }

    private void CloseItemDetails()
    {
        var returnScreen = _itemDetailsReturnScreen;
        // FND-UI-053: Sell and Give go on from the frame they held when Item Information opened.
        // As for the gang panel below, the item panel's last pass took the tick a held exit face
        // kept (FND-UI-047).
        if (_equipmentRotationHeld is { } held)
            _equipmentRotationStart = _eventPump.TicksAfterHold(_inputTime) - held;
        _equipmentRotationHeld = null;
        _itemDetailsId = null;
        _itemDetailsReturnScreen = ClientScreen.Commands;
        // FND-GANG-006: the gang information panel's rotation restarts at frame 0 when the item's
        // panel returns to it. A release of the held exit face leaves the kept tick to the item
        // panel's last pass (FND-UI-047), so the gang panel's counter does not take it.
        if (returnScreen == ClientScreen.Gang)
            _gangDetailsAnimationStart = _eventPump.TicksAfterHold(_inputTime);
        _screens.Show(returnScreen);
    }

    private void DrawItemDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_itemDetailsReturnScreen == ClientScreen.Gang)
            DrawGangDetails(batch, pixel, font, state);
        else if (_itemDetailsReturnScreen == ClientScreen.Give)
            DrawGiveEquipment(batch, pixel, font, state);
        else if (_itemDetailsReturnScreen == ClientScreen.Sell)
            DrawSellEquipment(batch, pixel, font, state);
        else
            DrawCommands(batch, pixel, font, state);
        if (ItemInfoBackground is not null)
            batch.Draw(ItemInfoBackground, ItemInformationLayout.Panel,
                ItemInformationLayout.BackgroundSource, Color.White);
        else
            batch.Draw(pixel, ItemInformationLayout.Panel, new Color(0, 0, 0, 245));
        if (_itemDetailsId is not { } itemId) return;

        var item = state.Definitions.Items[itemId];
        ClearItemInformationFields(batch, pixel);
        if (itemId >= 0 && itemId < ItemRotationTextures.Length
            && ItemRotationTextures[itemId] is { } rotation)
            batch.Draw(rotation, ItemInformationLayout.Portrait,
                // FND-UI-047: the rotation stops while the exit face is held. FND-UI-052: it starts
                // from frame 0 when the panel opens, or at the frame the reference frame's capture showed.
                // DEV-UI-027: Steady Lights holds frame 0.
                _referenceFrame?.ItemFrame is { } frame
                    ? ItemRotationPresentation.Frame(frame)
                    : ItemRotationPresentation.Frame((_steadyLights || PresentationDrawTime < _itemDetailsOpenedAt)
                        ? TimeSpan.Zero : PresentationDrawTime - _itemDetailsOpenedAt), Color.White);
        else if (ItemPortraits is not null)
            batch.Draw(ItemPortraits, ItemInformationLayout.CompactPortrait,
                OriginalSpriteLayout.ItemPortrait(item.Id), Color.White);
        font.Draw(batch, item.Name,
            new Vector2(ItemInformationLayout.NameLeft, ItemInformationLayout.HeaderY), Color.Lime, 1);
        DrawPanelValue(font, batch, ExecutableStrings.Get(ItemInformationLayout.TypeStringBase + item.Type),
            ItemInformationLayout.TypeRight, ItemInformationLayout.HeaderY);
        foreach (var entry in ItemInformationLayout.DescriptionLines(item.Description)
                     .Select((text, row) => (text, row)))
            font.Draw(batch, entry.text,
                new Vector2(ItemInformationLayout.DescriptionLeft,
                    ItemInformationLayout.DescriptionY + entry.row * 9), Color.Lime, 1);

        DrawItemPanelValue(font, batch, item.Cost, ItemInformationLayout.LeftValueLeft, 216,
            NativeTwoCellNumberPresentation.Kind.Baseline);
        DrawItemPanelValue(font, batch, item.TechLevel, ItemInformationLayout.RightValueLeft, 216,
            NativeTwoCellNumberPresentation.Kind.Baseline);
        int[] left =
        [
            item.Stats.Combat, item.Stats.Defense, item.Stats.Chaos, item.Stats.Control,
            item.Stats.Heal, item.Stats.Influence, item.Stats.Research
        ];
        int[] right =
        [
            item.Stats.Stealth, item.Stats.Detect, item.Stats.Strength, item.Stats.Blade,
            item.Stats.Range, item.Stats.Fighting, item.Stats.MartialArts
        ];
        for (var row = 0; row < left.Length; row++)
        {
            var y = ItemInformationLayout.StatisticY(row);
            DrawItemPanelValue(font, batch, left[row], ItemInformationLayout.LeftValueLeft, y);
            DrawItemPanelValue(font, batch, right[row], ItemInformationLayout.RightValueLeft, y);
        }
        if (TooltipHoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.ItemAt(hover));
    }

    private static void ClearItemInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(ItemInformationLayout.NameLeft, ItemInformationLayout.HeaderY, 114, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(ItemInformationLayout.TypeRight - 36, ItemInformationLayout.HeaderY, 36, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(ItemInformationLayout.DescriptionLeft, ItemInformationLayout.DescriptionY, 180, 25), Color.Black);
        ClearItemValueField(batch, pixel, ItemInformationLayout.LeftValueLeft, 216);
        ClearItemValueField(batch, pixel, ItemInformationLayout.RightValueLeft, 216);
        for (var row = 0; row < 7; row++)
        {
            var y = ItemInformationLayout.StatisticY(row);
            ClearItemValueField(batch, pixel, ItemInformationLayout.LeftValueLeft, y);
            ClearItemValueField(batch, pixel, ItemInformationLayout.RightValueLeft, y);
        }
    }

    private static void DrawItemPanelValue(PixelFont font, SpriteBatch batch, int value, int left, int y,
        NativeTwoCellNumberPresentation.Kind kind = NativeTwoCellNumberPresentation.Kind.Modifier)
    {
        var display = NativeTwoCellNumberPresentation.Format(value, kind);
        font.DrawNumber(batch, display,
            new Vector2(GangInformationLayout.ValueTextLeft(left, display.Digits), y),
            display.IsNegative ? Color.Red : Color.Lime);
    }

    private static void ClearItemValueField(SpriteBatch batch, Texture2D pixel, int left, int y) =>
        batch.Draw(pixel, GangInformationLayout.ValueField(left, y), Color.Black);
}

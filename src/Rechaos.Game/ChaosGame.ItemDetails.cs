using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenItemDetails(short itemId, ClientScreen returnScreen = ClientScreen.Commands)
    {
        _itemDetailsId = itemId;
        _itemDetailsReturnScreen = returnScreen;
        _screens.Show(ClientScreen.ItemInformation);
    }

    private void CloseItemDetails()
    {
        var returnScreen = _itemDetailsReturnScreen;
        _itemDetailsId = null;
        _itemDetailsReturnScreen = ClientScreen.Commands;
        _screens.Show(returnScreen);
    }

    private void DrawItemDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_itemDetailsReturnScreen == ClientScreen.Gang)
            DrawGangDetails(batch, pixel, font, state);
        else
            DrawCommands(batch, pixel, font, state);
        if (_itemInfoBackground is not null)
            batch.Draw(_itemInfoBackground, ItemInformationLayout.Panel,
                ItemInformationLayout.BackgroundSource, Color.White);
        else
            batch.Draw(pixel, ItemInformationLayout.Panel, new Color(0, 0, 0, 245));
        if (_itemDetailsId is not { } itemId) return;

        var item = state.Definitions.Items[itemId];
        ClearItemInformationFields(batch, pixel);
        if (itemId >= 0 && itemId < _itemRotationTextures.Length
            && _itemRotationTextures[itemId] is { } rotation)
            batch.Draw(rotation, ItemInformationLayout.Portrait,
                ItemRotationPresentation.Frame(_inputTime), Color.White);
        else if (_itemPortraits is not null)
            batch.Draw(_itemPortraits, ItemInformationLayout.CompactPortrait,
                OriginalSpriteLayout.ItemPortrait(item.Id), Color.White);
        font.Draw(batch, item.Name,
            new Vector2(ItemInformationLayout.NameLeft, ItemInformationLayout.HeaderY), Color.Lime, 1);
        DrawPanelValue(font, batch, ItemInformationLayout.TypeLabel(item.Type),
            ItemInformationLayout.TypeRight, ItemInformationLayout.HeaderY);
        foreach (var entry in ItemInformationLayout.DescriptionLines(item.Description)
                     .Select((text, row) => (text, row)))
            font.Draw(batch, entry.text,
                new Vector2(ItemInformationLayout.DescriptionLeft,
                    ItemInformationLayout.DescriptionY + entry.row * 9), Color.Lime, 1);

        DrawItemPanelValue(font, batch, item.Cost, ItemInformationLayout.LeftValueLeft, 216);
        DrawItemPanelValue(font, batch, item.TechLevel, ItemInformationLayout.RightValueLeft, 216);
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
        if (_hoverPoint is { } hover)
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

    private static void DrawItemPanelValue(PixelFont font, SpriteBatch batch, int value, int left, int y)
    {
        var display = ItemInformationLayout.FormatNumericValue(value);
        font.Draw(batch, display.Digits,
            new Vector2(GangInformationLayout.ValueTextLeft(left, display.Digits), y),
            display.IsNegative ? Color.Red : Color.Lime, 1);
    }

    private static void ClearItemValueField(SpriteBatch batch, Texture2D pixel, int left, int y) =>
        batch.Draw(pixel, GangInformationLayout.ValueField(left, y), Color.Black);
}

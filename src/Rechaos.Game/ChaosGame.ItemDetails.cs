using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenItemDetails(short itemId)
    {
        _itemDetailsId = itemId;
        _screens.Show(ClientScreen.ItemInformation);
    }

    private void CloseItemDetails()
    {
        _itemDetailsId = null;
        _screens.Show(ClientScreen.Commands);
    }

    private void DrawItemDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawCommands(batch, pixel, font, state);
        if (_itemInfoBackground is not null)
            batch.Draw(_itemInfoBackground, ItemInformationLayout.Panel, Color.White);
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
        font.Draw(batch, item.Name, new Vector2(199, 152), Color.Lime, 1);
        DrawPanelValue(font, batch, ItemInformationLayout.TypeLabel(item.Type),
            ItemInformationLayout.RightValueRight, 152);
        foreach (var entry in WrapPanelText(item.Description, ItemInformationLayout.DescriptionColumns).Take(3)
                     .Select((text, row) => (text, row)))
            font.Draw(batch, entry.text, new Vector2(199, 170 + entry.row * 9), Color.Lime, 1);

        DrawPanelValue(font, batch, item.Cost, ItemInformationLayout.LeftValueRight, 217);
        DrawPanelValue(font, batch, item.TechLevel, ItemInformationLayout.RightValueRight, 217);
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
            DrawPanelValue(font, batch, left[row], ItemInformationLayout.LeftValueRight, y);
            DrawPanelValue(font, batch, right[row], ItemInformationLayout.RightValueRight, y);
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.ItemAt(hover));
    }

    private static void ClearItemInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(198, 152, 146, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(347, 152, 36, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(198, 170, 185, 25), Color.Black);
        batch.Draw(pixel, new Rectangle(275, 217, 12, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(371, 217, 12, 7), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = ItemInformationLayout.StatisticY(row);
            batch.Draw(pixel, new Rectangle(275, y, 12, 7), Color.Black);
            batch.Draw(pixel, new Rectangle(371, y, 12, 7), Color.Black);
        }
    }
}

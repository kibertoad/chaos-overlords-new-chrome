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
        font.Draw(batch, item.Name,
            new Vector2(SharedPanelLayout.X(95), SharedPanelLayout.Y(27)), Color.Lime, 1);
        DrawPanelValue(font, batch, ItemInformationLayout.TypeLabel(item.Type),
            ItemInformationLayout.RightValueRight, SharedPanelLayout.Y(27));
        foreach (var entry in WrapPanelText(item.Description, ItemInformationLayout.DescriptionColumns).Take(3)
                     .Select((text, row) => (text, row)))
            font.Draw(batch, entry.text,
                new Vector2(SharedPanelLayout.X(95),
                    SharedPanelLayout.Y(45 + entry.row * 9)), Color.Lime, 1);

        DrawPanelValue(font, batch, item.Cost, ItemInformationLayout.LeftValueRight,
            SharedPanelLayout.Y(92));
        DrawPanelValue(font, batch, item.TechLevel, ItemInformationLayout.RightValueRight,
            SharedPanelLayout.Y(92));
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
        batch.Draw(pixel, SharedPanelLayout.At(94, 27, 146, 7), Color.Black);
        batch.Draw(pixel, SharedPanelLayout.At(243, 27, 36, 7), Color.Black);
        batch.Draw(pixel, SharedPanelLayout.At(94, 45, 185, 25), Color.Black);
        batch.Draw(pixel, SharedPanelLayout.At(171, 92, 12, 7), Color.Black);
        batch.Draw(pixel, SharedPanelLayout.At(267, 92, 12, 7), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = ItemInformationLayout.StatisticY(row);
            batch.Draw(pixel, new Rectangle(275, y, 12, 7), Color.Black);
            batch.Draw(pixel, new Rectangle(371, y, 12, 7), Color.Black);
        }
    }
}

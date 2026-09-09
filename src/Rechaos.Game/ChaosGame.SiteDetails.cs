using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenSiteDetails(int sectorId, int slot, ClientScreen returnScreen)
    {
        _siteDetailsSectorId = sectorId;
        _siteDetailsSlot = slot;
        _siteDetailsReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Site);
    }

    private void CloseSiteDetails()
    {
        var returnScreen = _siteDetailsReturnScreen;
        _siteDetailsSectorId = null;
        _siteDetailsSlot = null;
        _screens.Show(returnScreen);
    }

    private void DrawSiteDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_siteDetailsReturnScreen == ClientScreen.Commands)
            DrawCommands(batch, pixel, font, state);
        else
            DrawSectorDetails(batch, pixel, font, state);

        if (_siteInfoBackground is not null)
            batch.Draw(_siteInfoBackground, SiteInformationLayout.Panel, Color.White);
        else
            batch.Draw(pixel, SiteInformationLayout.Panel, new Color(0, 0, 0, 245));
        if (_siteDetailsSectorId is not { } sectorId || _siteDetailsSlot is not { } slot) return;

        var site = state.Sectors[sectorId].Sites.Single(value => value.Slot == slot);
        var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
        ClearSiteInformationFields(batch, pixel);
        if (_sitePortraits is not null)
            batch.Draw(_sitePortraits, SiteInformationLayout.Portrait,
                OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
        font.Draw(batch, definition.Name, new Vector2(263, 153), Color.Lime, 1);

        int[] data = [site.Resistance, definition.Tolerance, definition.Support, definition.Cash];
        for (var row = 0; row < data.Length; row++)
            DrawPanelValue(font, batch, data[row], SiteInformationLayout.DataValueRight,
                SiteInformationLayout.DataY(row));
        int[] left =
        [
            definition.Stats.Combat, definition.Stats.Defense, definition.Stats.Chaos,
            definition.Stats.Control, definition.Stats.Heal, definition.Stats.Influence,
            definition.Stats.Research
        ];
        int[] right =
        [
            definition.Stats.Stealth, definition.Stats.Detect, definition.Stats.Strength,
            definition.Stats.Blade, definition.Stats.Range, definition.Stats.Fighting,
            definition.Stats.MartialArts
        ];
        for (var row = 0; row < left.Length; row++)
        {
            var y = SiteInformationLayout.StatisticY(row);
            DrawPanelValue(font, batch, left[row], SiteInformationLayout.LeftValueRight, y);
            DrawPanelValue(font, batch, right[row], SiteInformationLayout.RightValueRight, y);
        }
    }

    private static void ClearSiteInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(262, 152, 121, 10), Color.Black);
        for (var row = 0; row < 4; row++)
            batch.Draw(pixel, new Rectangle(371, SiteInformationLayout.DataY(row), 12, 7), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = SiteInformationLayout.StatisticY(row);
            batch.Draw(pixel, new Rectangle(275, y, 12, 7), Color.Black);
            batch.Draw(pixel, new Rectangle(371, y, 12, 7), Color.Black);
        }
    }
}

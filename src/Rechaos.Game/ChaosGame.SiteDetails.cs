using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenSiteDetails(int sectorId, int slot, ClientScreen returnScreen)
    {
        _siteDetailsSectorId = sectorId;
        _siteDetailsSlot = slot;
        _siteDetailsDefinitionId = null;
        _siteDetailsReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Site);
    }

    private void OpenSiteDefinitionDetails(short definitionId, ClientScreen returnScreen)
    {
        if (_definitions?.Sites.Any(site => site.Id == definitionId) != true)
            throw new ArgumentOutOfRangeException(nameof(definitionId));
        _siteDetailsSectorId = null;
        _siteDetailsSlot = null;
        _siteDetailsDefinitionId = definitionId;
        _siteDetailsReturnScreen = returnScreen;
        _screens.Show(ClientScreen.Site);
    }

    private void CloseSiteDetails()
    {
        var returnScreen = _siteDetailsReturnScreen;
        _siteDetailsSectorId = null;
        _siteDetailsSlot = null;
        _siteDetailsDefinitionId = null;
        _screens.Show(returnScreen);
    }

    private void DrawSiteDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_siteDetailsReturnScreen == ClientScreen.Commands)
            DrawCommands(batch, pixel, font, state);
        else if (_siteDetailsReturnScreen == ClientScreen.Search)
            DrawSearch(batch, pixel, font, state);
        else
            DrawSectorDetails(batch, pixel, font, state);

        if (_siteInfoBackground is not null)
            batch.Draw(_siteInfoBackground, SiteInformationLayout.Panel, Color.White);
        else
            batch.Draw(pixel, SiteInformationLayout.Panel, new Color(0, 0, 0, 245));
        MatchSiteState? site = null;
        SiteDefinition definition;
        if (_siteDetailsDefinitionId is { } definitionId)
        {
            definition = state.Definitions.Sites.Single(value => value.Id == definitionId);
        }
        else
        {
            if (_siteDetailsSectorId is not { } sectorId || _siteDetailsSlot is not { } slot) return;
            site = state.Sectors[sectorId].Sites.Single(value => value.Slot == slot);
            definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
        }
        ClearSiteInformationFields(batch, pixel);
        if (_sitePortraits is not null)
            batch.Draw(_sitePortraits, SiteInformationLayout.Portrait,
                OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
        font.Draw(batch, definition.Name,
            new Vector2(SharedPanelLayout.X(159), SharedPanelLayout.Y(28)), Color.Lime, 1);

        int[] data =
        [
            site?.Resistance ?? definition.Resistance,
            definition.Tolerance,
            definition.Support,
            definition.Cash
        ];
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
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.SiteAt(hover));
    }

    private static void ClearSiteInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, SharedPanelLayout.At(158, 27, 121, 10), Color.Black);
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

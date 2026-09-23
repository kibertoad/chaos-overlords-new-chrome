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
            batch.Draw(_siteInfoBackground, SiteInformationLayout.Panel,
                SiteInformationLayout.BackgroundSource, Color.White);
        else
            batch.Draw(pixel, SiteInformationLayout.Panel, new Color(0, 0, 0, 245));
        MatchSiteState? site = null;
        SiteDefinition definition;
        if (_siteDetailsDefinitionId is { } definitionId)
        {
            definition = state.Definitions.Site(definitionId);
        }
        else
        {
            if (_siteDetailsSectorId is not { } sectorId || _siteDetailsSlot is not { } slot) return;
            site = state.Sectors[sectorId].Sites.Single(value => value.Slot == slot);
            definition = state.Definitions.Site(site.DefinitionId);
        }
        ClearSiteInformationFields(batch, pixel);
        if (_sitePortraits is not null)
            batch.Draw(_sitePortraits, SiteInformationLayout.Portrait,
                OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
        font.Draw(batch, definition.Name,
            new Vector2(SiteInformationLayout.NameLeft, 151), Color.Lime, 1);

        int[] data =
        [
            site?.Resistance ?? definition.Resistance,
            definition.Tolerance,
            definition.Support,
            definition.Cash
        ];
        for (var row = 0; row < data.Length; row++)
            DrawNativeTwoCellValue(font, batch, data[row], SiteInformationLayout.DataValueLeft,
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
            DrawNativeTwoCellValue(font, batch, left[row], SiteInformationLayout.LeftValueLeft, y);
            DrawNativeTwoCellValue(font, batch, right[row], SiteInformationLayout.RightValueLeft, y);
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.SiteAt(hover));
    }

    private static void ClearSiteInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(SiteInformationLayout.NameLeft, 151, 121, 10), Color.Black);
        for (var row = 0; row < 4; row++)
            batch.Draw(pixel, GangInformationLayout.ValueField(
                SiteInformationLayout.DataValueLeft, SiteInformationLayout.DataY(row)), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = SiteInformationLayout.StatisticY(row);
            batch.Draw(pixel, GangInformationLayout.ValueField(SiteInformationLayout.LeftValueLeft, y), Color.Black);
            batch.Draw(pixel, GangInformationLayout.ValueField(SiteInformationLayout.RightValueLeft, y), Color.Black);
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawFinance(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawMapBackdrop(batch, pixel, font, state, _managementReturnScreen);
        var playerId = ViewingPlayer(state);
        var player = state.FindPlayer(playerId)!;
        var background = _financeScope == FinanceScope.City
            ? _cityFinanceBackground
            : _sectorFinanceBackground;
        if (background is not null)
            batch.Draw(background, FinanceLayout.Panel, FinanceLayout.BackgroundSource, Color.White);
        else
            batch.Draw(pixel, FinanceLayout.Panel, new Color(0, 0, 0, 245));
        int? sectorId = _financeScope == FinanceScope.Sector ? _cursor : null;
        var projection = FinanceProjection.Project(state, player, sectorId);
        ClearFinanceFields(batch, pixel);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, FinanceLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        int[] rows =
        [
            projection.GangUpkeep,
            projection.NewContracts,
            projection.Equipment,
            projection.CityOfficials,
            projection.SectorTax,
            projection.SiteProtection,
            projection.ChaosEstimate,
            projection.CashAdjustment
        ];
        for (var index = 0; index < rows.Length; index++)
            DrawFinanceValue(font, batch, rows[index], FinanceLayout.ValueY(index));
        DrawNativeFixedWidthValue(font, batch, projection.ProjectedGangCount,
            FinanceLayout.ContractCountLeft, FinanceLayout.ContractCountY,
            FinanceLayout.ContractCountWidth(projection.ProjectedGangCount));
        font.Draw(batch, ")", new Vector2(
            FinanceLayout.ContractCountCloseLeft(projection.ProjectedGangCount), FinanceLayout.ContractCountY), Color.Lime, 1);
    }

    private static void DrawFinanceValue(
        PixelFont font, SpriteBatch batch, int value, int y) =>
        DrawNativeFixedWidthValue(font, batch, value, FinanceLayout.ValueLeft, y, 4);

    private static void ClearFinanceFields(SpriteBatch batch, Texture2D pixel)
    {
        for (var row = 0; row < FinanceLayout.RowCount; row++)
            batch.Draw(pixel, FinanceLayout.ValueField(row), Color.Black);
        batch.Draw(pixel, new Rectangle(FinanceLayout.ContractCountLeft,
            FinanceLayout.ContractCountY, 2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight), Color.Black);
    }

    private void DrawSearch(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawMapBackdrop(batch, pixel, font, state, _managementReturnScreen);
        DrawPanelArtwork(batch, pixel, _siteSearchBackground, SiteSearchLayout.Panel);

        var sites = state.Definitions.Sites.OrderBy(site => site.Id)
            .Take(SiteSearchLayout.MaximumSites).ToArray();
        var selection = _siteSearchSelections.For(
            ViewingPlayer(state));
        for (var index = 0; index < sites.Length; index++)
        {
            var row = SiteSearchLayout.Site(index);
            var selected = selection.Contains(sites[index].Id);
            if (index == _siteSearchCursor) DrawBorder(batch, pixel, row, Color.Gold, 1);
            DrawBorder(batch, pixel, new Rectangle(row.X + 2, row.Y + 2, 8, 8),
                selected ? Color.Lime : new Color(90, 100, 100), 1);
            if (selected)
                batch.Draw(pixel, new Rectangle(row.X + 4, row.Y + 4, 4, 4), Color.Lime);
            font.Draw(batch, sites[index].Name,
                new Vector2(row.X + 14, row.Y + 3), selected ? Color.Lime : Color.White, 1);
        }
    }

    private void OpenSiteSearch(ClientScreen returnScreen)
    {
        _managementReturnScreen = returnScreen;
        _siteSearchCursor = 0;
        _screens.Show(ClientScreen.Search);
    }

    private void MoveSiteSearchCursor(int delta)
    {
        if (_definitions is null || _definitions.Sites.Count == 0) return;
        var count = Math.Min(_definitions.Sites.Count, SiteSearchLayout.MaximumSites);
        _siteSearchCursor = (_siteSearchCursor + delta + count) % count;
    }

    private void ToggleSiteSearchSelection()
    {
        if (_definitions is null) return;
        var sites = _definitions.Sites.OrderBy(site => site.Id)
            .Take(SiteSearchLayout.MaximumSites).ToArray();
        if (_siteSearchCursor >= sites.Length) return;
        var id = sites[_siteSearchCursor].Id;
        _siteSearchSelections.Toggle(SiteSearchPlayer(), id);
    }

    private void SelectAllSiteSearch()
    {
        if (_definitions is null) return;
        AcceptInput();
        _siteSearchSelections.SelectAll(
            SiteSearchPlayer(),
            _definitions.Sites.OrderBy(site => site.Id)
                .Take(SiteSearchLayout.MaximumSites).Select(site => site.Id));
    }

    private void ClearSiteSearch()
    {
        AcceptInput();
        _siteSearchSelections.Clear(SiteSearchPlayer());
    }

    private void ApplySiteSearch()
    {
        AcceptInput();
        _message = string.Empty;
        _screens.Show(_managementReturnScreen);
    }

    private void CancelSiteSearch() => _screens.Show(_managementReturnScreen);

    private void HandleSiteSearchClick(Point point)
    {
        if (SiteSearchLayout.All.Contains(point)) SelectAllSiteSearch();
        else if (SiteSearchLayout.None.Contains(point)) ClearSiteSearch();
        else if (SiteSearchLayout.Ok.Contains(point)) ApplySiteSearch();
        else if (_definitions is not null)
        {
            var count = Math.Min(_definitions.Sites.Count, SiteSearchLayout.MaximumSites);
            for (var index = 0; index < count; index++)
            {
                if (!SiteSearchLayout.Site(index).Contains(point)) continue;
                _siteSearchCursor = index;
                var siteId = _definitions.Sites.OrderBy(site => site.Id)
                    .Take(SiteSearchLayout.MaximumSites).ElementAt(index).Id;
                if (_siteSearchClicks.Register(index, _inputTime))
                    OpenSiteDefinitionDetails(siteId, ClientScreen.Search);
                else
                    ToggleSiteSearchSelection();
                break;
            }
        }
    }

    private PlayerId SiteSearchPlayer() =>
        _state is null ? new PlayerId(0) : ViewingPlayer(_state);

    private void DrawRanking(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawMapBackdrop(batch, pixel, font, state, _managementReturnScreen);
        DrawRankingPanel(batch, pixel, font, state);
    }

    private void DrawRankingPanel(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _rankingBackground, PlayerRankingLayout.Panel);
        if (_uiSprites is null) return;
        var entries = PlayerRankingPresentation.Project(state);
        foreach (var entry in entries)
        {
            var player = state.FindPlayer(entry.Player)!;
            batch.Draw(_uiSprites,
                PlayerRankingLayout.Portrait(entry.Player.Value, entry.Standing),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, PlayerRankingTooltip.At(hover, state, entries));
    }
}

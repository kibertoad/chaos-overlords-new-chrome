using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawFinance(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_managementReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(batch, pixel, font, state);
        else
            DrawBoard(batch, pixel, font, state);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var background = _financeScope == FinanceScope.City
            ? _cityFinanceBackground
            : _sectorFinanceBackground;
        if (background is not null)
            batch.Draw(background, FinanceLayout.Panel, Color.White);
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
        font.Draw(batch, $"({projection.ProjectedGangCount})",
            new Vector2(FinanceLayout.ContractCountLeft, FinanceLayout.ValueY(1)), Color.Lime, 1);
    }

    private static void DrawFinanceValue(
        PixelFont font, SpriteBatch batch, int value, int y) =>
        DrawPanelValue(font, batch, Math.Abs(value).ToString(), FinanceLayout.ValueRight, y,
            value < 0 ? Color.Red : Color.Lime);

    private static void ClearFinanceFields(SpriteBatch batch, Texture2D pixel)
    {
        for (var row = 0; row < FinanceLayout.RowCount; row++)
            batch.Draw(pixel, new Rectangle(366, FinanceLayout.ValueY(row), 28, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(FinanceLayout.ContractCountLeft,
            FinanceLayout.ValueY(1), 42, 7), Color.Black);
    }

    private void DrawSearch(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_managementReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(batch, pixel, font, state);
        else
            DrawBoard(batch, pixel, font, state);
        if (_siteSearchBackground is not null)
            batch.Draw(_siteSearchBackground, SiteSearchLayout.Panel, Color.White);
        else
            batch.Draw(pixel, SiteSearchLayout.Panel, new Color(0, 0, 0, 245));

        var sites = state.Definitions.Sites.OrderBy(site => site.Id)
            .Take(SiteSearchLayout.MaximumSites).ToArray();
        for (var index = 0; index < sites.Length; index++)
        {
            var row = SiteSearchLayout.Site(index);
            var selected = _siteSearchSelection.Contains(sites[index].Id);
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
        _siteSearchSelection.Clear();
        _siteSearchSelection.UnionWith(_siteSearchApplied);
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
        if (!_siteSearchSelection.Remove(id)) _siteSearchSelection.Add(id);
    }

    private void SelectAllSiteSearch()
    {
        if (_definitions is null) return;
        _siteSearchSelection.Clear();
        _siteSearchSelection.UnionWith(_definitions.Sites
            .OrderBy(site => site.Id).Take(SiteSearchLayout.MaximumSites).Select(site => site.Id));
    }

    private void ClearSiteSearch() => _siteSearchSelection.Clear();

    private void ApplySiteSearch()
    {
        _siteSearchApplied.Clear();
        _siteSearchApplied.UnionWith(_siteSearchSelection);
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
                ToggleSiteSearchSelection();
                break;
            }
        }
    }

    private void DrawRanking(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_managementReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(batch, pixel, font, state);
        else
            DrawBoard(batch, pixel, font, state);
        if (_rankingBackground is not null)
            batch.Draw(_rankingBackground, PlayerRankingLayout.Panel, Color.White);
        else
            batch.Draw(pixel, PlayerRankingLayout.Panel, new Color(0, 0, 0, 245));
        if (_uiSprites is null) return;
        foreach (var entry in PlayerRankingPresentation.Project(state))
        {
            var player = state.FindPlayer(entry.Player)!;
            batch.Draw(_uiSprites,
                PlayerRankingLayout.Portrait(entry.Player.Value, entry.Standing),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        }
    }

    private void DrawManagementPanel(SpriteBatch batch, Texture2D pixel)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
    }

}

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
        // FND-FINANCE-002, FND-UI-025, EXP-UI-007: the Sector variant copies the sector's cell
        // from the unmarked copy of the city map, as Gangs in Sector does, and frames it in black.
        if (sectorId is { } tileSector)
            DrawUnmarkedSectorCell(batch, pixel, tileSector, FinanceLayout.SectorTile);
        else if (_uiSprites is not null)
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
        // SCR-FINANCE-001, FND-FINANCE-002: the Sector variant names the sector it totals.
        if (sectorId is { } financeSector)
            font.Draw(batch, SectorGangsLayout.SectorCodeText(financeSector),
                FinanceLayout.SectorName.ToVector2(), Color.Lime, 1);
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
            // DEV-SEARCH-001: the keyboard's row is outlined once a key has moved or flipped it.
            if (_siteSearchCursorShown && index == _siteSearchCursor) DrawBorder(batch, pixel, row, Color.Gold, 1);
            // FND-SEARCH-001, FND-SEARCH-004: the definition's marker icon, then the first 15
            // characters of its name, from the plain font when selected and the row at (152,274)
            // when not.
            var icon = SiteSearchLayout.Icon(index);
            if (_siteMarkerSprites is not null)
                batch.Draw(_siteMarkerSprites, icon, SiteSearchLayout.IconSource(sites[index].Id), Color.White);
            var name = sites[index].Name;
            font.Copy(batch, name[..Math.Min(name.Length, SiteSearchLayout.NameCharacters)],
                new Point(icon.X + 24, icon.Y + 3),
                selected ? OriginalFontLayout.PlainStrip : OriginalFontLayout.DimStrip);
        }
        DrawHeldSiteSearchFace(batch);
    }

    // DEV-SEARCH-001: whether a key has moved or flipped the Search panel's keyboard row since it
    // opened.
    private bool _siteSearchCursorShown;

    private int _siteSearchCursor;
    private readonly SiteSearchSelectionState _siteSearchSelections = new();
    private readonly IndexedDoubleClickTracker _siteSearchClicks = new();

    private void OpenSiteSearch(ClientScreen returnScreen)
    {
        _managementReturnScreen = returnScreen;
        _siteSearchCursor = 0;
        _siteSearchCursorShown = false;
        _screens.Show(ClientScreen.Search);
    }

    private void MoveSiteSearchCursor(int delta)
    {
        if (_definitions is null || _definitions.Sites.Count == 0) return;
        var count = Math.Min(_definitions.Sites.Count, SiteSearchLayout.MaximumSites);
        _siteSearchCursor = (_siteSearchCursor + delta + count) % count;
        _siteSearchCursorShown = true;
    }

    private short[] SiteSearchRows() => _definitions is null ? [] : SiteSearchPanel.Rows(_definitions);

    private void ToggleSiteSearchSelection()
    {
        var rows = SiteSearchRows();
        if (_siteSearchCursor >= rows.Length) return;
        // DEV-SEARCH-001: the row Space flips is outlined, so the key never acts on a row it hides.
        _siteSearchCursorShown = true;
        SiteSearchPanel.Apply(_siteSearchSelections, SiteSearchPlayer(),
            new SiteSearchPress(SiteSearchControl.Row, _siteSearchCursor), rows);
    }

    private void SelectAllSiteSearch()
    {
        if (_definitions is null) return;
        AcceptInput();
        SiteSearchPanel.Apply(_siteSearchSelections, SiteSearchPlayer(),
            new SiteSearchPress(SiteSearchControl.All), SiteSearchRows());
    }

    private void ClearSiteSearch()
    {
        AcceptInput();
        SiteSearchPanel.Apply(_siteSearchSelections, SiteSearchPlayer(),
            new SiteSearchPress(SiteSearchControl.None), []);
    }

    private void ApplySiteSearch()
    {
        AcceptInput();
        _message = string.Empty;
        CloseSiteSearch();
    }

    // FND-SEARCH-004: closing the panel draws the whole city again, gang-status markers included
    // (RULE-UI-006).
    private void CloseSiteSearch()
    {
        if (_state is { Coordinator.Phase: TurnPhase.Command } state)
        {
            var viewer = ViewingPlayer(state);
            _gangMarkers.RedrawAll(state, viewer, _gangSight.For(state, viewer));
        }
        _screens.Show(_managementReturnScreen);
    }

    private void HandleSiteSearchClick(Point point)
    {
        var rows = SiteSearchRows();
        var click = SiteSearchPanel.Press(_siteSearchSelections, SiteSearchPlayer(), point, rows,
            _siteSearchClicks, _inputTime);
        switch (click.Press.Control)
        {
            case SiteSearchControl.All:
            case SiteSearchControl.None:
            case SiteSearchControl.Done:
                // FND-UI-062: the face is held until the button comes up and acts only on a
                // release inside it.
                var held = click.Press.Control;
                PressPanelFace(point, SiteSearchLayout.Panel, SiteSearchLayout.Target(held),
                    () => ReleaseSiteSearchControl(held));
                break;
            case SiteSearchControl.Row:
                _siteSearchCursor = click.Press.Row;
                if (click.OpensDetails) OpenSiteDefinitionDetails(rows[click.Press.Row], ClientScreen.Search);
                break;
        }
    }

    /// <summary>A release inside the held ALL, NONE or Done, the only release the panel face hold acts on.</summary>
    private void ReleaseSiteSearchControl(SiteSearchControl held)
    {
        if (!SiteSearchPanel.Release(_siteSearchSelections, SiteSearchPlayer(), held, SiteSearchRows())) return;
        _message = string.Empty;
        CloseSiteSearch();
    }

    /// <summary>SCR-SEARCH-001, FND-UI-062: the lit face of a held ALL, NONE or Done while the pointer is over it.</summary>
    private void DrawHeldSiteSearchFace(SpriteBatch batch)
    {
        if (_uiSprites is null || _pressedPanelFace is not { Screen: ClientScreen.Search } held
            || _pressedPanelFaceByRightButton || _hoverPoint is not { } hover || !held.Face.Contains(hover))
            return;
        foreach (var control in (ReadOnlySpan<SiteSearchControl>)
                 [SiteSearchControl.All, SiteSearchControl.None, SiteSearchControl.Done])
            if (SiteSearchLayout.Target(control) == held.Face)
                batch.Draw(_uiSprites, SiteSearchLayout.HeldFace(control),
                    HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(control)), Color.White);
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
                PlayerRankingLayout.Portrait(entry),
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, PlayerRankingTooltip.At(hover, state, entries));
    }
}

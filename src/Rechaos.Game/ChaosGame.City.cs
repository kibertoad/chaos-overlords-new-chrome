using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void UpdateCity(KeyboardState keyboard)
    {
        if (_state is null) return;
        if (_idleGangWarningOpen)
        {
            UpdateIdleGangWarning(keyboard);
            return;
        }
        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.A)) MoveCursor(-1, 0);
        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.D)) MoveCursor(1, 0);
        if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W)) MoveCursor(0, -1);
        if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S)) MoveCursor(0, 1);
        // SCR-UI-003: Enter or Execute opens the detailed sector screen for the selected sector,
        // during planning.
        if (PressedEnterOrExecute(keyboard) && CityPlanningInputOpen()) OpenSectorDetails();
        if (Pressed(keyboard, Keys.C)) OpenCommands();
        if (Pressed(keyboard, Keys.G)) CycleGang(1);
        if (Pressed(keyboard, Keys.I)) OpenSectorDetails();
        if (Pressed(keyboard, Keys.F)) OpenFinance(FinanceScope.City, ClientScreen.City);
        if (Pressed(keyboard, Keys.R)) _screens.Show(ClientScreen.Ranking);
        if (Pressed(keyboard, Keys.T)) OpenItems();
        if (Pressed(keyboard, Keys.B)) OpenCombatResults(ClientScreen.City);
        if (Pressed(keyboard, Keys.X)) OpenSiteSearch(ClientScreen.City);
        if (Pressed(keyboard, Keys.H)) OpenHire();
        if (Pressed(keyboard, Keys.M)) OpenComlinkView(ClientScreen.City);
        if (Pressed(keyboard, Keys.N)) OpenComlinkSend(ClientScreen.City);
        if (Pressed(keyboard, Keys.J)) OpenManagement(ClientScreen.GameInfo, ClientScreen.City);
        if (Pressed(keyboard, Keys.Space)) AdvanceTurn();
        // Saves and replays belong to a locally authoritative match. The multiplayer server keeps
        // the match history after every turn, so it is resumed through Online instead.
        if (_session is null)
        {
            if (Pressed(keyboard, Keys.F5)) OpenSaveBrowser(saving: true);
            if (Pressed(keyboard, Keys.F9)) OpenSaveBrowser(saving: false);
            if (Pressed(keyboard, Keys.F6)) SaveReplay();
            if (Pressed(keyboard, Keys.F10)) LoadReplay();
        }
        else if (Pressed(keyboard, Keys.F6) || Pressed(keyboard, Keys.F10))
        {
            RejectInput("ONLINE MATCH CANNOT SAVE REPLAY");
        }
    }

    private void HandleCityClick(Point point)
    {
        if (_idleGangWarningOpen)
        {
            HandleIdleGangWarningClick(point);
            return;
        }
        var rejectSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.Reject, point);
        var hireSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.PortraitHit, point);
        if (rejectSlot >= 0)
        {
            BeginHireReject(rejectSlot, ClientScreen.City);
        }
        else if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
        }
        else if (CityMapLayout.TrySectorAt(point, out var selected))
        {
            _cursor = selected;
            _message = string.Empty;
            if (_citySectorClicks.Register(selected, _inputTime) && CityPlanningInputOpen())
                OpenSectorDetails();
        }
        else
        {
            _citySectorClicks.Cancel();
            BeginCityConsolePress(point, ClientScreen.City);
        }
    }

    /// <summary>
    /// SCR-UI-003: the city's keys and sector double-click are enabled during planning only, and
    /// the final view is a planning visit (FND-OBJECTIVE-004). A refused input says why, as the
    /// rebuild's message line does for the other refused inputs.
    /// </summary>
    private bool CityPlanningInputOpen()
    {
        if (_finalViewPlayer is not null) return true;
        if (_actions is null)
        {
            RejectInput(OnlinePlanningClosed);
            return false;
        }
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command)
        {
            RejectInput("SECTOR VIEW REQUIRES COMMAND PHASE");
            return false;
        }
        return true;
    }

    private bool BeginCityConsolePress(Point point, ClientScreen returnScreen)
    {
        if (CityConsoleLayout.HitTest(point) is not { } control) return false;
        _pressedCityConsoleControl = control;
        _pressedCityConsoleAction = CityConsoleLayout.ActionAt(point);
        _pressedCityConsoleReturnScreen = returnScreen;
        PlayGeneralSound(AudioRouting.PointerPushSound());
        return true;
    }

    private void CompleteCityConsolePress(Point point)
    {
        var control = _pressedCityConsoleControl;
        var action = _pressedCityConsoleAction;
        var returnScreen = _pressedCityConsoleReturnScreen;
        CancelCityConsolePress();
        if (control is null || action is null
            || _screens.Current != returnScreen
            || CityConsoleLayout.HitTest(point) != control)
            return;

        switch (action)
        {
            case CityConsoleAction.GameInfo:
                OpenManagement(ClientScreen.GameInfo, returnScreen);
                break;
            case CityConsoleAction.Done:
                AdvanceTurn();
                break;
            case CityConsoleAction.Events:
                OpenEvents(returnScreen);
                break;
            case CityConsoleAction.ComlinkView:
                OpenComlinkView(returnScreen);
                break;
            case CityConsoleAction.ComlinkSend:
                OpenComlinkSend(returnScreen);
                break;
            case CityConsoleAction.CombatSummary:
                OpenCombatResults(returnScreen);
                break;
            case CityConsoleAction.CombatDetail:
                OpenCombatDetail(returnScreen);
                break;
            case CityConsoleAction.FinanceCity:
                OpenFinance(FinanceScope.City, returnScreen);
                break;
            case CityConsoleAction.FinanceSector:
                OpenFinance(FinanceScope.Sector, returnScreen);
                break;
            case CityConsoleAction.Gangs:
                OpenSectorGangDetails(returnScreen);
                break;
            case CityConsoleAction.Hire:
                OpenHire(returnScreen);
                break;
            case CityConsoleAction.Ranking:
                OpenManagement(ClientScreen.Ranking, returnScreen);
                break;
            case CityConsoleAction.Search:
                OpenSiteSearch(returnScreen);
                break;
        }
    }

    private void CancelCityConsolePress()
    {
        _pressedCityConsoleControl = null;
        _pressedCityConsoleAction = null;
    }

    private void OpenManagement(ClientScreen screen, ClientScreen returnScreen)
    {
        _managementReturnScreen = returnScreen;
        _screens.Show(screen);
    }

    private void OpenFinance(FinanceScope scope, ClientScreen returnScreen)
    {
        _financeScope = scope;
        OpenManagement(ClientScreen.Finance, returnScreen);
    }

    /// <summary>
    /// The city screen's console and Overlord bar, with the city map unless
    /// <paramref name="sectorView"/> says the sector view is drawn over it.
    /// </summary>
    private void DrawBoard(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state, SectorViewFrame? sectorView = null)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        var playerIndex = PlanningViewer?.Value ?? 0;
        var player = state.Players[playerIndex];
        // FND-UI-017, FND-UI-018: the marker follows the viewed player, which on the sector view is
        // the player whose cards are shown, and there a portrait dims when the active player sees
        // none of that player's gangs in the sector.
        if (sectorView is { } view)
        {
            _overlordMarkerClock.SectorView(_cursor, view.Viewed, _inputTime);
            DrawOverlordBar(batch, pixel, state, view.Viewed, view.SeatsSeen);
        }
        else
        {
            _overlordMarkerClock.OtherView();
            DrawOverlordBar(batch, pixel, state, PlanningViewer, seatsSeen: null);
        }
        if (sectorView is null)
        {
            DrawPreparedCityMap(batch, pixel, state, player.Id,
                CityMapLayout.Bounds with { X = 0, Y = 0 }, CityMapLayout.Bounds.Location);
            if (_uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, CityMapLayout.Destination(_cursor),
                    CityMapLayout.SelectionFrameSource(CityMapLayout.SelectionFrame(_inputTime)), Color.White);
            else
                DrawBorder(batch, pixel, CityMapLayout.Destination(_cursor), Color.Gold, 2);
            // FND-UI-037: the city-cell flash lightens the cell copied from the map, markers
            // included, and the edge tabs are keyed over the lightening.
            DrawFlashLightening(batch, TickedPresentationKind.CityCellFlash);
            foreach (var label in CityMapLayout.GridLabels())
                DrawGridLabel(batch, font, label);
            if (_draggedHireDefinitionId is not null
                && CityMapLayout.TrySectorAt(_dragPoint, out var dropSector))
            {
                var friendlyGangs = player.Gangs.Where(
                    gang => gang.IsActive && gang.SectorId == dropSector).ToArray();
                var source = friendlyGangs.Length == 0
                    ? OriginalSpriteLayout.IncomingGangStatus
                    : GangStatusSource(state, player.Id, dropSector, friendlyGangs, hasPendingHire: true);
                DrawGangStatusMarker(batch, dropSector, source);
                DrawBorder(batch, pixel, CityMapLayout.Destination(dropSector),
                    state.Sectors[dropSector].Owner == player.Id ? Color.Lime : Color.OrangeRed, 2);
            }
        }

        var selectedSector = state.Sectors[_cursor];
        var selectedSectorChaos = ChaosRangeProjection.Detail(state, player.Id, _cursor);
        // FND-UI-040: separate calendar fields leave the template's separator intact.
        font.Draw(batch, ExecutableStrings.Get(ExecutableStrings.ScenarioNumber(state.Setup.Scenario) + 1),
            new Vector2(StatusConsoleLayout.ScenarioLeft, StatusConsoleLayout.ScenarioY), Color.Lime, 1);
        var (year, week) = MatchCalendar.Of(MatchCalendar.PresentationElapsedTurns(state));
        // FND-UI-004: numeric cells are opaque, including their blank pixels.
        batch.Draw(pixel, new Rectangle(StatusConsoleLayout.YearLeft, StatusConsoleLayout.DateY,
            4 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight), Color.Black);
        batch.Draw(pixel, new Rectangle(StatusConsoleLayout.WeekLeft, StatusConsoleLayout.DateY,
            2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight), Color.Black);
        DrawNativeFixedWidthValue(font, batch, year, StatusConsoleLayout.YearLeft,
            StatusConsoleLayout.DateY, 4);
        font.Draw(batch, week.ToString("00"),
            new Vector2(StatusConsoleLayout.WeekLeft, StatusConsoleLayout.DateY), Color.Lime, 1);
        // FND-UI-040, FND-STATE-010: completion replaces the timed countdown in the final view.
        if (_finalViewPlayer is not null)
            font.Draw(batch, ExecutableStrings.Get(19),
                new Vector2(StatusConsoleLayout.CompleteLeft, StatusConsoleLayout.DateY), Color.Lime, 1);
        else if (StatusConsolePresentation.RemainingTurns(state.Setup.Scenario, state.Setup.Duration,
                     state.Coordinator.Turn, finalView: false) is { } remainingTurns)
        {
            batch.Draw(pixel, new Rectangle(StatusConsoleLayout.RemainingTurnsLeft, StatusConsoleLayout.DateY,
                3 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight), Color.Black);
            DrawNativeFixedWidthValue(font, batch, remainingTurns, StatusConsoleLayout.RemainingTurnsLeft,
                StatusConsoleLayout.DateY, 3);
        }
        // FND-UI-040, RULE-UI-004: five opaque numeric cells, including red unsigned magnitudes.
        const int scoreLeft = 550;
        const int scoreWidth = 5;
        batch.Draw(pixel, new Rectangle(scoreLeft, StatusConsoleLayout.ScoreY,
            scoreWidth * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight), Color.Black);
        DrawNativeFixedWidthValue(font, batch, player.ScenarioScore, scoreLeft,
            StatusConsoleLayout.ScoreY, scoreWidth);
        DrawPanelValue(font, batch, StatusConsolePresentation.CashSummary(player.Cash,
                StatusConsolePresentation.UnspentCash(state, player),
                FinanceProjection.Project(state, player, sectorId: null).CashAdjustment),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.CashY);
        // RULE-UI-011: the sector code, the Income word and three two-cell numbers at x 568;
        // Support and Cash are 0 unless the active player owns the sector.
        var sectorValues = StatusConsolePresentation.SectorValues(state, player.Id, _cursor);
        font.Draw(batch, SectorCode(_cursor),
            new Vector2(StatusConsoleLayout.SectorValueLeft, StatusConsoleLayout.SectorValueY(0)), Color.Lime, 1);
        font.Draw(batch, StatusConsolePresentation.IncomeWord(sectorValues.Income),
            new Vector2(StatusConsoleLayout.SectorValueLeft, StatusConsoleLayout.SectorValueY(1)), Color.Lime, 1);
        DrawSectorNumber(font, batch, sectorValues.Tolerance, StatusConsoleLayout.SectorValueY(2),
            selectedSectorChaos.Range.CanTriggerCrackdown(selectedSector.Tolerance) ? Color.OrangeRed : Color.Lime);
        DrawSectorNumber(font, batch, sectorValues.Support, StatusConsoleLayout.SectorValueY(3), Color.Lime);
        // FND-UI-035: the sector Cash label is already present in the background art.
        DrawSectorNumber(font, batch, sectorValues.Cash, StatusConsoleLayout.SectorValueY(4), Color.Lime);
        font.Draw(batch, CityStatusMessage.Clip(_message),
            new Vector2(438, 354), Color.Gold, 1);
        if (_state is not null && PlanningViewer is { } reportPlayer
            && LastTurnReports(state, reportPlayer).Count > 0
            && PresentationClock.BlinkLit(_inputTime))
            DrawCityLight(batch, pixel, OriginalSelectionLightLayout.CityEvents);
        if (_state is not null && PlanningViewer is { } activePlayer
            && state.ComlinkFor(activePlayer).HasUnread
            && PresentationClock.BlinkLit(_inputTime))
            DrawCityLight(batch, pixel, OriginalSelectionLightLayout.CityComlinkView);
        // FND-EVENT-006, FND-UI-039: the Done light blinks through every final view.
        if (_finalViewPlayer is not null && PresentationClock.BlinkLit(_inputTime))
            DrawCityLight(batch, pixel, OriginalSelectionLightLayout.CityDone);
        DrawHireDock(batch, font, state, player);
        if (_hireDragStarted && _draggedHireDefinitionId is { } draggedDefinition && _gangPortraits is not null)
        {
            var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 18, 36, 36);
            batch.Draw(_gangPortraits, token,
                OriginalSpriteLayout.GangPortrait(draggedDefinition), Color.White);
            DrawBorder(batch, pixel, token, Color.White, 1);
        }
        // Online, the footer says where the turn stands instead of which keys save: a match nobody
        // can save is one where the only thing worth knowing is whether it is waiting on you.
        var footer = _session is null
            ? "ARROWS ENTER/H/SPACE  F5/F9 SAVE  F6/F10 REPLAY"
            : OnlineTurnStatus();
        font.Draw(batch, footer, new Vector2(18, 439), new Color(180, 190, 190), 1);
        DrawPressedCityConsole(batch);
        DrawStatusConsoleTooltip(batch, pixel, font);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
    }

    /// <summary>
    /// RULE-UI-011, RULE-UI-004: a sector value in two cells from x 568, red without its sign when
    /// negative, and with its whole leading quotient in the first cell when it is wider than two.
    /// <paramref name="color"/> is the colour of a value that is not negative (DEV-UI-007 turns
    /// Tolerance orange).
    /// </summary>
    private static void DrawSectorNumber(PixelFont font, SpriteBatch batch, int value, int y, Color color)
    {
        var display = NativeTwoCellNumberPresentation.Format(value, NativeTwoCellNumberPresentation.Kind.Baseline);
        font.Draw(batch, display.Digits,
            new Vector2(StatusConsoleLayout.SectorValueLeft + (2 - display.Digits.Length) * OriginalFontLayout.CellWidth, y),
            display.IsNegative ? Color.Red : color, 1);
    }

    /// <summary>A lit console light; a dark one is the console art under it (FND-EVENT-006).</summary>
    private void DrawCityLight(SpriteBatch batch, Texture2D pixel, Rectangle light)
    {
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, light, OriginalSelectionLightLayout.CityLightSource, Color.White);
        else
            DrawSelectionLight(batch, pixel, light);
    }

    private void DrawPressedCityConsole(SpriteBatch batch)
    {
        if (_uiSprites is not null
            && _pressedCityConsoleControl is { } pressed
            && _hoverPoint is { } hover
            && CityConsoleLayout.HitTest(hover) == pressed)
            batch.Draw(_uiSprites, CityConsoleLayout.Destination(pressed),
                CityConsoleLayout.PressedSource(pressed), Color.White);
    }

    private void DrawStatusConsoleTooltip(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_hoverPoint is { } statusHover && StatusConsoleTooltip.Contains(statusHover)
            && _state is { } state)
        {
            var player = ViewingPlayer(state);
            if (StatusConsoleLayout.Cash.Contains(statusHover))
            {
                DrawCashSpendingTooltip(batch, pixel, font, statusHover,
                    state, state.FindPlayer(player)!);
                return;
            }
            var sector = state.Sectors[_cursor];
            var chaosEstimate = ChaosRangeProjection.Detail(state, player, _cursor);
            var enemyGangsPresent = HasEnemyGang(state, player, _cursor);
            var lines = StatusConsoleTooltip.At(
                statusHover, state.Setup.Scenario, state.Setup.Duration, sector.Tolerance,
                chaosEstimate, StatusConsolePresentation.ChaosBreakdown(state, chaosEstimate),
                enemyGangsPresent, StatusConsoleTooltip.ToleranceParts.Of(state, sector));
            DrawHoverTooltip(batch, pixel, font, statusHover, lines,
                StatusConsoleTooltip.QueuedChaosRangeRow,
                StatusConsoleTooltip.QueuedChaosRangePrefix,
                StatusConsolePresentation.QueuedChaosRangeColor(chaosEstimate.Range, sector.Tolerance),
                enemyGangsPresent ? StatusConsoleTooltip.EnemyChaosWarningRow : null,
                enemyGangsPresent ? Color.Red : null);
        }
    }

    private static void DrawCashSpendingTooltip(SpriteBatch batch, Texture2D pixel, PixelFont font,
        Point point, MatchState state, MatchPlayerState player)
    {
        const int maxRowsPerColumn = 40;
        var spends = StatusConsolePresentation.QueuedCashSpends(state, player);
        var header = StatusConsolePresentation.CashTooltip(
            player.Cash, spends, FinanceProjection.Project(state, player, sectorId: null));
        string[] rows = spends.Count == 0
            ? ["NONE"]
            : spends.Select(entry =>
                $"{entry.Position:00} {entry.GangName} {entry.Description} ${entry.Price}").ToArray();
        var rowCount = Math.Min(maxRowsPerColumn, rows.Length);
        var columnCount = (rows.Length + maxRowsPerColumn - 1) / maxRowsPerColumn;
        var columnWidth = rows.Max(row => row.Length) * OriginalFontLayout.CellWidth + 12;
        var width = Math.Max(header.Max(line => line.Length) * OriginalFontLayout.CellWidth + 16,
            columnCount * columnWidth + 16);
        var height = (header.Count + rowCount) * OriginalFontLayout.LineHeight + 16;
        var x = Math.Clamp(point.X + 10, 4, Math.Max(4, VirtualInput.Width - width - 4));
        var y = Math.Clamp(point.Y + 12, 4, Math.Max(4, VirtualInput.Height - height - 4));
        var panel = new Rectangle(x, y, width, height);
        batch.Draw(pixel, panel, new Color(8, 18, 16, 252));
        DrawBorder(batch, pixel, panel, Color.Lime, 2);
        for (var row = 0; row < header.Count; row++)
            font.Draw(batch, header[row],
                new Vector2(x + 8, y + 8 + row * OriginalFontLayout.LineHeight),
                row == 0 ? Color.Gold : Color.White, 1);
        for (var index = 0; index < rows.Length; index++)
            font.Draw(batch, rows[index],
                new Vector2(x + 8 + index / maxRowsPerColumn * columnWidth,
                    y + 8 + (header.Count + index % maxRowsPerColumn)
                    * OriginalFontLayout.LineHeight), Color.White, 1);
    }

    private static bool HasEnemyGang(MatchState state, PlayerId viewer, int sectorId) =>
        state.Players.Where(player => player.Id != viewer)
            .SelectMany(player => player.Gangs)
            .Any(gang => gang.IsActive
                && gang.SectorId == sectorId);

    private void DrawGangStatusMarker(SpriteBatch batch, int sectorId, Rectangle source)
    {
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, GangStatusMarkerLayout.Destination(sectorId), source, Color.White);
    }

    /// <summary>
    /// Copies one sector's cell of the prepared city map to <paramref name="topLeft"/>, with what
    /// the map holds there for the active player: ground, owner's art, pylons, site markers and
    /// gang marker (SCR-UI-005, FND-UI-014).
    /// </summary>
    private void DrawCitySectorCell(
        SpriteBatch batch, Texture2D pixel, MatchState state, int sectorId, Point topLeft) =>
        DrawPreparedCityMap(batch, pixel, state, state.Players[PlanningViewer?.Value ?? 0].Id,
            CityMapLayout.Source(sectorId), topLeft);

    private Rectangle GangStatusSource(
        MatchState state,
        PlayerId viewer,
        int sectorId,
        IEnumerable<MatchGangState> friendlyGangs,
        bool hasPendingHire)
    {
        var hasIdleGang = friendlyGangs.Any(gang => gang.QueuedCommand is null);
        // RULE-UI-006: enemy sight comes from the snapshot taken when planning started.
        var hasDetectedEnemyGang = _gangSight.For(state, viewer).EnemySeen(sectorId);
        return GangStatusMarkerPresentation.Source(
            hasIdleGang, hasDetectedEnemyGang, hasPendingHire);
    }

    private void MoveCursor(int dx, int dy)
    {
        var x = Math.Clamp(_cursor % MatchLimits.BoardWidth + dx, 0, MatchLimits.BoardWidth - 1);
        var y = Math.Clamp(_cursor / MatchLimits.BoardWidth + dy, 0, MatchLimits.BoardWidth - 1);
        _cursor = y * MatchLimits.BoardWidth + x;
        _message = string.Empty;
    }

    private static string SectorCode(int sectorId) =>
        $"{(char)('A' + sectorId % MatchLimits.BoardWidth)}{sectorId / MatchLimits.BoardWidth + 1}";

    private static string MatchDate(int turn)
    {
        var (year, week) = MatchCalendar.Of(Math.Max(0, turn - 1));
        return $"{year}.{week:00}";
    }

}

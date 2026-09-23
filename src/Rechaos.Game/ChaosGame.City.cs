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
        if (Pressed(keyboard, Keys.Enter)) QueueBoardCommand();
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
        var hireSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.Portrait, point);
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
            if (_citySectorClicks.Register(selected, _inputTime)) OpenSectorDetails();
        }
        else
        {
            _citySectorClicks.Cancel();
            BeginCityConsolePress(point, ClientScreen.City);
        }
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

    private void DrawBoard(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state, bool drawMapLayer = true)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        if (_uiSprites is not null)
        {
            foreach (var setupPlayer in state.Setup.Players)
                batch.Draw(_uiSprites, PlayerPortraitLayout.CityTop(setupPlayer.Id.Value),
                    OriginalSpriteLayout.OverlordPortrait(setupPlayer.PortraitId), Color.White);
            if (state.Coordinator.ActivePlayer is { } turnPlayer)
                batch.Draw(_uiSprites, PlayerPortraitLayout.CityActiveMarker(turnPlayer.Value),
                    OriginalSpriteLayout.ActivePlayerMarker(
                        ActivePlayerMarkerPresentation.Frame(_inputTime)), Color.White);
        }
        DrawSeatPlanning(batch, pixel, font);
        var playerIndex = state.Coordinator.ActivePlayer?.Value ?? 0;
        var player = state.Players[playerIndex];
        if (drawMapLayer)
        {
            var neutralLayer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(null)];
            if (neutralLayer is not null)
                batch.Draw(neutralLayer, CityMapLayout.Bounds, Color.White);
            for (var index = 0; index < state.Sectors.Count; index++)
            {
                var sector = state.Sectors[index];
                var destination = CityMapLayout.Destination(index);
                var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
                if (sector.Owner is not null && layer is not null)
                    batch.Draw(layer, CityMapLayout.OwnershipDestination(index),
                        CityMapLayout.OwnershipSource(index), Color.White);
                else if (neutralLayer is null)
                    batch.Draw(pixel, destination, sector.Owner is { } owner
                        ? PlayerColors[owner.Value] * .68f
                        : new Color(24, 37, 39));
                if (_uiKeyedSprites is not null
                    && ObjectiveSectorMarkerPresentation.IsMarked(
                        state.Setup.Scenario, index, sector.IsImportant))
                    batch.Draw(_uiKeyedSprites, destination,
                        OriginalSpriteLayout.ObjectiveSectorPylons, Color.White);
            }
            foreach (var marker in CitySiteMarkerProjection.Project(
                         state, player.Id, _siteSearchSelections.For(player.Id)))
            {
                var destination = CitySiteMarkerProjection.Destination(marker);
                if (_siteMarkerSprites is not null)
                    batch.Draw(_siteMarkerSprites, destination,
                        CitySiteMarkerProjection.Source(marker), Color.White);
                else
                    DrawBorder(batch, pixel, destination,
                        marker.Controlled ? Color.Lime : Color.Cyan, 1);
            }
        }
        DrawBorder(batch, pixel, CityMapLayout.Destination(_cursor), Color.Gold, 2);
        var activeGangsBySector = player.Gangs.Where(gang => gang.IsActive)
            .GroupBy(gang => gang.SectorId).ToArray();
        var pendingHireSectors = player.PendingHires
            .Select(pending => pending.TargetSectorId).ToHashSet();
        foreach (var gangs in activeGangsBySector)
            DrawGangStatusMarker(batch, gangs.Key,
                GangStatusSource(state, player.Id, gangs.Key, gangs,
                    pendingHireSectors.Contains(gangs.Key)));
        var occupiedGangSectors = activeGangsBySector.Select(gangs => gangs.Key).ToHashSet();
        foreach (var pendingSector in pendingHireSectors.Where(
                     pendingSector => !occupiedGangSectors.Contains(pendingSector)))
            DrawGangStatusMarker(batch, pendingSector, OriginalSpriteLayout.IncomingGangStatus);
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

        var selectedSector = state.Sectors[_cursor];
        var selectedSectorChaos = ChaosRangeProjection.Detail(state, player.Id, _cursor);
        var scenario = ScenarioCatalog.Get(state.Setup.Scenario);
        batch.Draw(pixel, new Rectangle(StatusConsoleLayout.LabelLeft, 14, 44, 8), Color.Black);
        font.Draw(batch, scenario.Name,
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.ScenarioY), Color.Lime, 1);
        font.Draw(batch, MatchDate(state.Coordinator.Turn),
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.DateY), Color.Lime, 1);
        DrawPanelValue(font, batch, ScenarioScore(state, player).ToString(),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.ScoreY);
        var projectedCashflow = FinanceProjection.Project(state, player, sectorId: null).CashAdjustment;
        DrawPanelValue(font, batch, StatusConsolePresentation.Cash(player.Cash, projectedCashflow),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.CashY);
        DrawPanelValue(font, batch, SectorCode(_cursor),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(0));
        DrawPanelValue(font, batch, $"${SectorIncome(state, selectedSector)}",
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(1));
        DrawPanelValue(font, batch, selectedSector.Tolerance.ToString(),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(2),
            selectedSectorChaos.Range.CanTriggerCrackdown(selectedSector.Tolerance)
                ? Color.OrangeRed
                : Color.Lime);
        DrawPanelValue(font, batch, SectorSupport(state, player.Id, selectedSector),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(3));
        batch.Draw(pixel, StatusConsoleLayout.CashLabel, Color.Black);
        font.Draw(batch, "CASH",
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.SectorValueY(4)),
            Color.Lime, 1);
        DrawPanelValue(font, batch,
            StatusConsolePresentation.SectorCash(
                selectedSector.Owner, player.Id, SectorIncomeResolver.SectorCash(state, selectedSector)),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(4));
        font.Draw(batch, CityStatusMessage.Clip(_message),
            new Vector2(438, 354), Color.Gold, 1);
        if (state.Coordinator.ActivePlayer is { } reportPlayer
            && LastTurnReports(state, reportPlayer).Count > 0
            && (int)(_inputTime.TotalMilliseconds / 350) % 2 == 0)
            DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.CityEvents);
        if (state.Coordinator.ActivePlayer is { } activePlayer
            && state.ComlinkFor(activePlayer).HasUnread
            && (int)(_inputTime.TotalMilliseconds / 350) % 2 == 0)
            DrawSelectionLight(batch, pixel, OriginalSelectionLightLayout.CityComlinkView);
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
            ? "OPTIONS > KEYS TO VIEW SHORTCUTS  CLICK PANELS"
            : OnlineTurnStatus();
        font.Draw(batch, footer, new Vector2(18, 439), new Color(180, 190, 190), 1);
        DrawPressedCityConsole(batch);
        DrawStatusConsoleTooltip(batch, pixel, font);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
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
            var sector = state.Sectors[_cursor];
            var chaosEstimate = ChaosRangeProjection.Detail(state, player, _cursor);
            var enemyGangsPresent = HasEnemyGang(state, player, _cursor);
            var lines = StatusConsoleTooltip.At(
                statusHover, state.Setup.Scenario, state.Setup.Duration, sector.Tolerance,
                chaosEstimate, StatusConsolePresentation.ChaosBreakdown(state, chaosEstimate),
                enemyGangsPresent);
            DrawHoverTooltip(batch, pixel, font, statusHover, lines,
                StatusConsoleTooltip.QueuedChaosRangeRow,
                StatusConsoleTooltip.QueuedChaosRangePrefix,
                StatusConsolePresentation.QueuedChaosRangeColor(chaosEstimate.Range, sector.Tolerance),
                enemyGangsPresent ? StatusConsoleTooltip.EnemyChaosWarningRow : null,
                enemyGangsPresent ? Color.Red : null);
        }
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

    private static Rectangle GangStatusSource(
        MatchState state,
        PlayerId viewer,
        int sectorId,
        IEnumerable<MatchGangState> friendlyGangs,
        bool hasPendingHire)
    {
        var hasIdleGang = friendlyGangs.Any(gang => gang.QueuedCommand is null);
        var hasDetectedEnemyGang = state.Players
            .Where(player => player.Id != viewer)
            .SelectMany(player => player.Gangs)
            .Any(gang => gang.IsActive
                && gang.SectorId == sectorId
                && state.CanPlayerDetectGang(viewer, gang.Id));
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

    private void QueueBoardCommand()
    {
        if (_actions is null)
        {
            RejectInput(OnlinePlanningClosed);
            return;
        }
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            RejectInput("BOARD REQUIRES COMMAND PHASE");
            return;
        }
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null)
        {
            RejectInput("NO ACTIVE GANG");
            return;
        }
        var command = gang.SectorId == _cursor
            ? new GameCommand(playerId, gang.Id, GangAction.Control, CommandTarget.None)
            : new GameCommand(playerId, gang.Id, GangAction.Move, CommandTarget.Sector(_cursor));
        var result = _actions.Submit(command);
        ReportInputResult(result.Accepted, result.Validation.Message);
    }

    private static int SectorIncome(MatchState state, MatchSectorState sector) => sector.Income;

    private static string SectorCode(int sectorId) =>
        $"{(char)('A' + sectorId % MatchLimits.BoardWidth)}{sectorId / MatchLimits.BoardWidth + 1}";

    private static string MatchDate(int turn)
    {
        var week = Math.Max(0, turn - 1);
        return $"{2050 + week / 52}.{week % 52 + 1:00}";
    }

    private static long ScenarioScore(MatchState state, MatchPlayerState player)
    {
        var definition = ScenarioCatalog.Get(state.Setup.Scenario);
        return definition.IsTimed
            ? ScenarioCatalog.TimedScore(state.Setup.Scenario, state.Setup.Duration,
                MatchOutcomeEvaluator.Project(state, player))
            : 0;
    }

    private static int SectorSupport(MatchState state, PlayerId player, MatchSectorState sector) =>
        sector.Sites.Where(site => site.InfluencedBy == player)
            .Sum(site => state.Definitions.Site(site.DefinitionId).Support);
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle CityDone = new(492, 278, 106, 54);
    private static readonly Rectangle CityGameInfo = new(588, 40, 30, 54);
    private static readonly Rectangle CityEvents = new(492, 124, 50, 51);
    private static readonly Rectangle CityComlinkView = new(548, 124, 50, 25);
    private static readonly Rectangle CityComlinkSend = new(548, 150, 50, 25);
    private static readonly Rectangle CityCombatSummary = CityConsoleLayout.CombatSummary;
    private static readonly Rectangle CityFinanceCity = new(548, 176, 50, 32);
    private static readonly Rectangle CityFinanceSector = new(548, 208, 50, 17);
    private static readonly Rectangle CityGangs = new(492, 226, 50, 34);
    private static readonly Rectangle CityHire = new(492, 260, 50, 17);
    private static readonly Rectangle CityCombatDetail = CityConsoleLayout.CombatDetail;
    private static readonly Rectangle CityRanking = CityConsoleLayout.Ranking;
    private static readonly Rectangle CitySearch = new(548, 260, 50, 17);

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
        if (Pressed(keyboard, Keys.I)) _screens.Show(ClientScreen.Sector);
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
        if (Pressed(keyboard, Keys.F5)) OpenSaveBrowser(saving: true);
        if (Pressed(keyboard, Keys.F9)) OpenSaveBrowser(saving: false);
        // Replays belong to a locally authoritative match. Slot saves may still capture an online
        // match as a labelled offline snapshot, while replaying speculative online state may not.
        if (_session is null)
        {
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
        var rejectSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Reject(slot).Contains(point), -1);
        var hireSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Portrait(slot).Contains(point), -1);
        if (rejectSlot >= 0)
        {
            SnubHireDockOffer(rejectSlot, pointerButton: true);
        }
        else if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
        }
        else if (CityMapLayout.TrySectorAt(point, out var selected))
        {
            _cursor = selected;
            _message = string.Empty;
            if (_citySectorClicks.Register(selected, _inputTime))
            {
                _screens.Show(ClientScreen.Sector);
                _message = string.Empty;
            }
        }
        else
        {
            _citySectorClicks.Cancel();
            HandleCityConsoleClick(point, ClientScreen.City);
        }
    }

    private bool HandleCityConsoleClick(Point point, ClientScreen returnScreen)
    {
        if (!IsCityConsoleControl(point)) return false;
        PlayGeneralSound(AudioRouting.PointerPushSound());
        if (CityGameInfo.Contains(point)) OpenManagement(ClientScreen.GameInfo, returnScreen);
        else if (CityDone.Contains(point)) AdvanceTurn();
        else if (CityEvents.Contains(point)) OpenEvents(returnScreen);
        else if (CityComlinkView.Contains(point)) OpenComlinkView(returnScreen);
        else if (CityComlinkSend.Contains(point)) OpenComlinkSend(returnScreen);
        else if (CityCombatSummary.Contains(point)) OpenCombatResults(returnScreen);
        else if (CityFinanceCity.Contains(point)) OpenFinance(FinanceScope.City, returnScreen);
        else if (CityFinanceSector.Contains(point)) OpenFinance(FinanceScope.Sector, returnScreen);
        else if (CityGangs.Contains(point)) OpenSectorGangDetails(returnScreen);
        else if (CityHire.Contains(point)) OpenHire(returnScreen);
        else if (CityCombatDetail.Contains(point)) OpenCombatDetail(returnScreen);
        else if (CityRanking.Contains(point)) OpenManagement(ClientScreen.Ranking, returnScreen);
        else if (CitySearch.Contains(point)) OpenSiteSearch(returnScreen);
        return true;
    }

    private static bool IsCityConsoleControl(Point point) =>
        CityGameInfo.Contains(point)
        || CityDone.Contains(point)
        || CityEvents.Contains(point)
        || CityComlinkView.Contains(point)
        || CityComlinkSend.Contains(point)
        || CityCombatSummary.Contains(point)
        || CityFinanceCity.Contains(point)
        || CityFinanceSector.Contains(point)
        || CityGangs.Contains(point)
        || CityHire.Contains(point)
        || CityCombatDetail.Contains(point)
        || CityRanking.Contains(point)
        || CitySearch.Contains(point);

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

    private void DrawBoard(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
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
        var playerIndex = state.Coordinator.ActivePlayer?.Value ?? 0;
        var player = state.Players[playerIndex];
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
            if (state.Setup.Scenario == ScenarioId.Siege && sector.IsImportant)
                DrawSiegePylons(batch, pixel, index);
            if (sector.CrackdownActive && _policeSprites is not null)
                batch.Draw(
                    _policeSprites,
                    new Rectangle(destination.X + 14, destination.Y + 10, 27, 32),
                    OriginalSpriteLayout.PolicePatrolCar,
                    Color.White);
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
        DrawBorder(batch, pixel, CityMapLayout.Destination(_cursor), Color.Gold, 2);
        foreach (var gangs in player.Gangs.Where(gang => gang.IsActive).GroupBy(gang => gang.SectorId))
            DrawGangStatusMarker(batch, gangs.Key,
                gangs.Any(gang => gang.QueuedCommand is not null)
                    ? OriginalSpriteLayout.AssignedGangStatus
                    : OriginalSpriteLayout.IdleGangStatus);
        foreach (var pending in player.PendingHires)
            DrawGangStatusMarker(batch, pending.TargetSectorId, OriginalSpriteLayout.IncomingGangStatus);
        if (_draggedHireDefinitionId is not null
            && CityMapLayout.TrySectorAt(_dragPoint, out var dropSector))
        {
            DrawGangStatusMarker(batch, dropSector, OriginalSpriteLayout.IncomingGangStatus);
            DrawBorder(batch, pixel, CityMapLayout.Destination(dropSector),
                state.Sectors[dropSector].Owner == player.Id ? Color.Lime : Color.OrangeRed, 2);
        }

        var selectedSector = state.Sectors[_cursor];
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
        DrawPanelValue(font, batch, selectedSector.Tolerance,
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(2));
        DrawPanelValue(font, batch, SectorSupport(state, player.Id, selectedSector),
            StatusConsoleLayout.ValueRight, StatusConsoleLayout.SectorValueY(3));
        batch.Draw(pixel, StatusConsoleLayout.ChaosLabel, Color.Black);
        font.Draw(batch, "CHAOS",
            new Vector2(StatusConsoleLayout.LabelLeft, StatusConsoleLayout.SectorValueY(4)),
            Color.Lime, 1);
        DrawPanelValue(font, batch, selectedSector.Chaos,
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
            ? "ARROWS ENTER/H/SPACE  F5/F9 SAVE  F6/F10 REPLAY"
            : OnlineTurnStatus();
        font.Draw(batch, footer, new Vector2(18, 439), new Color(180, 190, 190), 1);
        DrawStatusConsoleTooltip(batch, pixel, font);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
    }

    private void DrawStatusConsoleTooltip(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_hoverPoint is { } statusHover)
            DrawHoverTooltip(batch, pixel, font, statusHover, StatusConsoleTooltip.At(statusHover));
    }

    private void DrawGangStatusMarker(SpriteBatch batch, int sectorId, Rectangle source)
    {
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, GangStatusMarkerLayout.Destination(sectorId), source, Color.White);
    }

    private static void DrawSiegePylons(SpriteBatch batch, Texture2D pixel, int sectorId)
    {
        foreach (var pylon in SiegePylonLayout.ForSector(sectorId))
        {
            batch.Draw(pixel, new Rectangle(pylon.X - 1, pylon.Y, pylon.Width + 2, 2), Color.LightGray);
            batch.Draw(pixel, pylon, Color.Gray);
            batch.Draw(pixel, new Rectangle(
                pylon.X + 1, pylon.Y + 2, pylon.Width - 2, pylon.Height - 3), Color.LightGray);
        }
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

    private static int SectorIncome(MatchState state, MatchSectorState sector) =>
        SectorIncomeResolver.OperationalIncome(state, sector);

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
            .Sum(site => state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Support);
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// The opponent whose gangs the Sector workspace lists instead of the viewer's own. Cleared
    /// when the screen is opened afresh or the selected sector changes, so the workspace always
    /// starts on the viewer's own gangs.
    /// </summary>
    private PlayerId? _sectorGangCardOwner;

    /// <summary>Opens the Sector workspace on the selected sector and the viewer's own gangs.</summary>
    private void OpenSectorDetails()
    {
        _sectorGangCardOwner = null;
        _gangSelection.Clear();
        _message = string.Empty;
        _screens.Show(ClientScreen.Sector);
    }

    private void UpdateSector(KeyboardState keyboard)
    {
        if (_idleGangWarningOpen)
        {
            UpdateIdleGangWarning(keyboard);
            return;
        }
        var column = _cursor % 8;
        var row = _cursor / 8;
        var previousCursor = _cursor;
        if (Pressed(keyboard, Keys.Left) && column > 0) _cursor--;
        if (Pressed(keyboard, Keys.Right) && column < 7) _cursor++;
        if (Pressed(keyboard, Keys.Up) && row > 0) _cursor -= 8;
        if (Pressed(keyboard, Keys.Down) && row < 7) _cursor += 8;
        if (_cursor != previousCursor) _sectorGangCardOwner = null;
        _gangSelection.KeepOnly(_cursor);
        if (Pressed(keyboard, Keys.Back))
            _screens.Show(ClientScreen.City);
        // FND-UI-015: Enter and Execute show the back control pressed for one tick of the
        // presentation clock, then return to the city (fn_00418CCC kind 3, RULE-TIMER-004).
        else if (PressedEnterOrExecute(keyboard))
            PressKeyFace(PressedKeyFace.SectorBack, SectorDetailLayout.Back.Location,
                () => _screens.Show(ClientScreen.City));
    }

    /// <summary>
    /// What one draw of the sector view reads more than once, taken once: the cards it lists, the
    /// player they belong to, whom the Overlord bar's marker follows (FND-UI-018:
    /// <c>0x00487B8C</c>), and for each seat whether the viewer sees one of its gangs here.
    /// </summary>
    private readonly record struct SectorViewFrame(
        IReadOnlyList<MatchGangState> Cards, PlayerId Viewed, IReadOnlyList<bool> SeatsSeen);

    private SectorViewFrame ComposeSectorView(MatchState state, PlayerId viewer)
    {
        var cards = SectorCardGangs(state, viewer);
        return new SectorViewFrame(cards, cards.Count > 0 ? cards[0].Owner : viewer,
            SectorOpponentGangs.SeenSeats(state, viewer, _cursor));
    }

    /// <summary>
    /// SCR-UI-004, FND-UI-018: the group order strip is drawn when the cards show at least two
    /// gangs of the player whose turn it is, during planning.
    /// </summary>
    private bool ShowsGroupOrderStrip(MatchState state, PlayerId viewer, IReadOnlyList<MatchGangState> cards) =>
        ShowsGroupOrderStrip(state, PlanningViewer, viewer, cards);

    internal static bool ShowsGroupOrderStrip(
        MatchState state, PlayerId? planningViewer, PlayerId viewer, IReadOnlyList<MatchGangState> cards) =>
        state.Coordinator.Phase == TurnPhase.Command
        && planningViewer == viewer
        && cards.Count >= 2
        && cards[0].Owner == viewer;

    /// <summary>The gangs the Sector workspace lists (<see cref="SectorOpponentGangs.Cards"/>).</summary>
    private IReadOnlyList<MatchGangState> SectorCardGangs(MatchState state, PlayerId viewer) =>
        SectorOpponentGangs.Cards(state, viewer, _sectorGangCardOwner, _cursor);

    /// <summary>
    /// Points the workspace at an overlord's gangs (RULE-UI-010, <see cref="SectorOpponentGangs.PressPortrait"/>).
    /// </summary>
    private void SelectSectorGangCardOwner(MatchState state, PlayerId viewer, PlayerId owner)
    {
        _message = string.Empty;
        _sectorGangCardOwner = SectorOpponentGangs.PressPortrait(state, viewer, _sectorGangCardOwner, owner, _cursor);
        // Borrowing an opponent's cards puts the player's own gangs out of sight, and a pick
        // nobody can see is a pick nobody meant to keep. That holds for a press on the overlord
        // already borrowed whose cards had fallen back to the viewer's own.
        if (!SectorOpponentGangs.Detectable(state, viewer, owner, _cursor)) return;
        _gangSelection.Clear();
    }

    private void HandleSectorClick(Point point)
    {
        if (_idleGangWarningOpen)
        {
            HandleIdleGangWarningClick(point);
            return;
        }
        // A press of the other button ends a right double-click, as it does in Windows.
        _sectorRightClicks.Cancel();
        HandleSectorPress(point, rightButton: false);
    }

    /// <summary>
    /// SCR-UI-004, FND-UI-063: a right press on the sector view. While the left button holds the
    /// back control, the original is in that control's helper loop, which takes no right press.
    /// The second press of a right double-click reaches the original as a double-click event,
    /// which only the console tiles take there.
    /// </summary>
    private void HandleSectorRightPress(Point point)
    {
        if (_pressedPanelFace is not null) return;
        if (_sectorRightClicks.Register(point, _inputTime))
        {
            if (_state is not null) BeginCityConsolePress(point, ClientScreen.Sector, rightButton: true);
            return;
        }
        HandleSectorPress(point, rightButton: true);
    }

    /// <summary>
    /// SCR-UI-004, FND-UI-063: the sector view takes a right press where it takes a left one. The
    /// console tiles are held until the right button comes up. The back control and an offer's
    /// reject cross act at the press, since their held-button helper waits only on the left
    /// button, and an offer's or a card's portrait starts no drag. The neighbouring cells and the
    /// sites take only a left double-click.
    /// </summary>
    private void HandleSectorPress(Point point, bool rightButton)
    {
        if (PressSectorBack(point, rightButton) || _state is null) return;
        var playerId = ViewingPlayer(_state);
        if (SectorOpponentGangs.PortraitAt(_state, point) is { } portraitOwner)
        {
            SelectSectorGangCardOwner(_state, playerId, portraitOwner);
            return;
        }
        if (BeginCityConsolePress(point, ClientScreen.Sector, rightButton)) return;
        var rejectSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.Reject, point);
        var hireSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.PortraitHit, point);
        if (rejectSlot >= 0)
        {
            BeginHireReject(rejectSlot, ClientScreen.Sector);
            if (rightButton) CompleteHireReject(point);
            return;
        }
        if (hireSlot >= 0)
        {
            if (!rightButton) BeginHireDrag(hireSlot, point);
            return;
        }
        if (!rightButton && SectorDetailLayout.TrySectorAt(point, _cursor, out var selectedSector))
        {
            // SCR-UI-004: only a double-click on a neighbouring cell selects it.
            if (!_sectorNeighborClicks.Register(selectedSector, _inputTime)) return;
            if (selectedSector != _cursor) _sectorGangCardOwner = null;
            _cursor = selectedSector;
            _gangSelection.KeepOnly(_cursor);
            _message = string.Empty;
            return;
        }
        var siteSlot = rightButton ? -1 : SectorDetailLayout.SiteAt(point);
        if (siteSlot >= 0)
        {
            if (_sectorSiteClicks.Register(_cursor * MatchLimits.SitesPerSector + siteSlot, _inputTime))
                OpenSiteDetails(_cursor, siteSlot, ClientScreen.Sector);
            return;
        }
        var cards = SectorCardGangs(_state, playerId);
        if (SectorDetailLayout.GroupOrderStrip.Contains(point))
        {
            if (ShowsGroupOrderStrip(_state, playerId, cards))
                OpenGroupCommands(_state, playerId,
                    SectorDetailLayout.GroupOrderIsRecurring(point));
            return;
        }
        PressSectorCard(_state, playerId, cards, point, rightButton);
    }

    /// <summary>
    /// FND-UI-015: the back control returns to the city when a left press is released inside it.
    /// The held-button helper plays slot 3 when the press starts (FND-AUDIO-011). It waits only on
    /// the left button, so a right press returns at once (FND-UI-063).
    /// </summary>
    private bool PressSectorBack(Point point, bool rightButton)
    {
        if (!SectorDetailLayout.Back.Contains(point)) return false;
        AcceptInput();
        if (rightButton) _screens.Show(ClientScreen.City);
        else
            _pressedPanelFace = (SectorDetailLayout.Back, ClientScreen.Sector,
                () => _screens.Show(ClientScreen.City));
        return true;
    }

    /// <summary>
    /// A press on a card of the workspace (SCR-UI-004, FND-UI-015). The right button acts as the
    /// left does, except that it picks up no drag: the rebuild's card drag (DEV-UI-022) follows the
    /// left button.
    /// </summary>
    private void PressSectorCard(
        MatchState state, PlayerId playerId, IReadOnlyList<MatchGangState> cards, Point point, bool rightButton)
    {
        var index = SectorGangCardLayout.CardAt(point);
        if (index < 0 || index >= Math.Min(cards.Count, SectorGangCardLayout.VisibleCards)) return;
        var gang = cards[index];
        if (gang.Owner != playerId)
        {
            PressOpponentCard(gang, index, point);
            return;
        }
        var ownGangs = state.FindPlayer(playerId)!.Gangs.Where(candidate => candidate.IsActive).ToArray();
        _selectedGangIndex = Array.FindIndex(ownGangs, candidate => candidate.Id == gang.Id);
        if (_multiSelectModifier)
        {
            ToggleGangSelection(gang);
            return;
        }
        var repeat = SectorGangCardLayout.ActionRepeatAt(index, point);
        if (repeat is { } selectedRepeat)
        {
            if (IsSelectedForBulkCommand(gang)) OpenBulkCommands(selectedRepeat);
            else OpenCommands(selectedRepeat, ClientScreen.Sector);
        }
        else if (!rightButton && SectorGangCardLayout.Portrait(index).Contains(point))
            BeginGangDrag(gang, point);
        else
            RegisterGangCardClick(gang);
    }

    /// <summary>
    /// FND-UI-015: on another player's card a double-click on the portrait opens the gang panel
    /// and one on an equipment icon opens Item Information (fn_004169B3).
    /// </summary>
    private void PressOpponentCard(MatchGangState gang, int index, Point point)
    {
        _message = string.Empty;
        var equipped = EquippedItems(gang);
        var onPortrait = SectorGangCardLayout.Portrait(index).Contains(point);
        var itemSlot = SectorGangCardLayout.ItemSlotAt(index, point);
        if (!onPortrait && (itemSlot < 0 || equipped[itemSlot] is null)) return;
        var region = onPortrait ? 0 : itemSlot + 1;
        if (!_sectorGangClicks.Register(SectorGangCardLayout.ClickKey(gang.Id, region), _inputTime)) return;
        if (onPortrait) OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
        else OpenItemDetails(equipped[itemSlot]!.Value, ClientScreen.Sector);
    }

    /// <summary>A click on the gang of a card, which opens its panel on the second one.</summary>
    private void RegisterGangCardClick(MatchGangState gang)
    {
        if (_sectorGangClicks.Register(SectorGangCardLayout.ClickKey(gang.Id, 0), _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
    }

    private void DrawSectorDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        var viewer = ViewingPlayer(state);
        var view = ComposeSectorView(state, viewer);
        DrawBoard(batch, pixel, font, state, view);
        var sector = state.Sectors[_cursor];
        DrawSectorBackground(batch, pixel);
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, SectorDetailLayout.OwnerStrip,
                SectorDetailLayout.OwnerStripSource(sector.Owner), Color.White);
            batch.Draw(_uiSprites, SectorDetailLayout.BackStrip, SectorDetailLayout.BackStripSource, Color.White);
        }
        else
            font.Draw(batch, "<", new Vector2(8, 410), Color.Lime, 2);
        if (_pressedPanelFace is { } held && held.Face == SectorDetailLayout.Back
            && _hoverPoint is { } backHover && SectorDetailLayout.Back.Contains(backHover))
            DrawPressedSectorBack(batch);
        DrawSectorNeighborhood(batch, pixel, font, state);
        // FND-UI-018, FND-UI-070: each site is its portrait under the keyed frame, with the progress
        // meter only when the sector's owner is the active player, whoever the viewed player is.
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Site(site.DefinitionId);
            var portrait = SectorDetailLayout.SitePortrait(site.Slot);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, portrait,
                    OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
            // FND-UI-037: the site flash lightens the image; the frame and meter go on top unlit.
            if (_tickedPresentation.Area == portrait)
                DrawFlashLightening(batch, TickedPresentationKind.SiteFlash);
            if (_uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, portrait, SectorDetailLayout.SitePortraitFrameSource, Color.White);
            else
                DrawBorder(batch, pixel, portrait, Color.Gray, 1);
            if (sector.Owner != viewer) continue;
            var meter = SectorDetailLayout.SiteControlBar(site.Slot);
            var filled = SectorDetailLayout.SiteControlWidth(definition.Resistance, site.Resistance);
            if (_uiSprites is not null)
            {
                if (filled > 0)
                    batch.Draw(_uiSprites, meter with { Width = filled },
                        SectorDetailLayout.SiteControlBarSource(filled), Color.White);
            }
            else
                DrawSectorMeter(batch, pixel, meter, filled, new Color(0, 247, 0));
        }
        var visibleGangs = view.Cards;
        var showsGroupOrderStrip = ShowsGroupOrderStrip(state, viewer, visibleGangs);
        foreach (var entry in visibleGangs.Take(SectorGangCardLayout.VisibleCards)
                     .Select((gang, index) => (gang, index)))
            DrawSectorGangCard(batch, pixel, font, state, viewer, entry.gang, entry.index);
        if (_uiSprites is not null && showsGroupOrderStrip)
            batch.Draw(_uiSprites, SectorDetailLayout.GroupOrderStrip,
                OriginalSpriteLayout.GroupOrderStrip, Color.White);
        DrawQueuedCommandTargetHighlight(batch, pixel, viewer, visibleGangs);
        DrawGangMoveDrag(batch, pixel, state);
        DrawSectorHireDrag(batch, pixel, state);
        // The Sector workspace covers the left side of right-edge tooltips drawn by
        // DrawBoard, so composite the tooltip again after the workspace is complete.
        DrawStatusConsoleTooltip(batch, pixel, font);
        // RULE-TURN-005: what the group order strip does, as FND-TURN-009 records it.
        if (_hoverPoint is { } stripHover && showsGroupOrderStrip
            && SectorDetailLayout.GroupOrderStrip.Contains(stripHover))
            DrawHoverTooltip(batch, pixel, font, stripHover,
            [
                "GROUP ORDER",
                "GIVES ONE ORDER TO EVERY GANG YOU HAVE HERE, HIDING OR NOT.",
                "LEFT PART: FOR THIS TURN. LAST QUARTER ON THE",
                "RIGHT: RECURRING, WITHOUT RESEARCH.",
                "HEAL SKIPS GANGS AT FORCE 10. A GANG THAT CANNOT",
                "TAKE THE ORDER KEEPS ITS PREVIOUS ONE."
            ]);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
    }

    private void DrawPressedSectorBack(SpriteBatch batch)
    {
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, SectorDetailLayout.Back,
                PressedKeyFaces.Source(PressedKeyFace.SectorBack), Color.White);
    }

    private Texture2D? _sectorBackgroundShade;

    /// <summary>
    /// FND-UI-018: the sector's 52-by-50 cell interior in the unmarked map, stretched over the map
    /// area and darkened with black through bitmap 143, the pattern the grey 0x8000 selects, laid
    /// from the area's corner.
    /// </summary>
    private void DrawSectorBackground(SpriteBatch batch, Texture2D pixel)
    {
        var area = CityMapLayout.Bounds;
        var neutral = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(null)];
        if (neutral is not null)
            batch.Draw(neutral, area, CityMapLayout.OwnershipSource(_cursor), Color.White);
        else
            batch.Draw(pixel, area, new Color(24, 37, 39));
        if (_sectorBackgroundShade is null)
        {
            _sectorBackgroundShade = new Texture2D(GraphicsDevice, area.Width, area.Height);
            _sectorBackgroundShade.SetData(OriginalPatternMask.ShadedRectangle(
                OriginalPatternMask.ForGrey(0x8000), area.Width, area.Height, Color.Black, Color.Black));
        }
        batch.Draw(_sectorBackgroundShade, area, Color.White);
    }

    private void DrawQueuedCommandTargetHighlight(
        SpriteBatch batch,
        Texture2D pixel,
        PlayerId viewer,
        IReadOnlyList<MatchGangState> visibleGangs)
    {
        if (_hoverPoint is not { } point) return;
        // The card a press here would take (FND-UI-015), gaps between the cards included.
        var hoveredSlot = SectorGangCardLayout.CardAt(point);
        // An opponent's orders stay their own business: the cards hide their action strip, so the
        // workspace must not betray the same order by highlighting what it targets.
        if (hoveredSlot < 0 || hoveredSlot >= Math.Min(visibleGangs.Count, SectorGangCardLayout.VisibleCards)
            || visibleGangs[hoveredSlot].Owner != viewer
            || visibleGangs[hoveredSlot].QueuedCommand is not { } queued) return;

        switch (queued.Command.Action)
        {
            case GangAction.Move:
                for (var column = 0; column < SectorDetailLayout.Columns; column++)
                for (var row = 0; row < SectorDetailLayout.Rows; row++)
                    if (SectorDetailLayout.SectorAt(_cursor, column, row) == queued.Command.Target.Id)
                        DrawBorder(batch, pixel, SectorDetailLayout.Cell(column, row), Color.White, 2);
                break;
            case GangAction.Control:
                // Control always works the gang's own sector, which the 3-by-3 crop centers.
                DrawBorder(batch, pixel, SectorDetailLayout.Cell(1, 1), Color.White, 2);
                break;
            case GangAction.Influence:
                if (queued.Command.Target.Id / MatchLimits.SitesPerSector == _cursor)
                    DrawBorder(batch, pixel,
                        SectorDetailLayout.SitePortrait(queued.Command.Target.Id % MatchLimits.SitesPerSector),
                        Color.White, 2);
                break;
            case GangAction.Attack:
                var targetSlot = Enumerable.Range(0,
                        Math.Min(visibleGangs.Count, SectorGangCardLayout.VisibleCards))
                    .FirstOrDefault(slot => visibleGangs[slot].Id.Value == queued.Command.Target.Id, -1);
                if (targetSlot >= 0)
                    DrawBorder(batch, pixel, SectorGangCardLayout.Frame(targetSlot), Color.White, 2);
                break;
        }
    }

    private void DrawSectorHireDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_hireDragStarted || _draggedHireDefinitionId is not { } definitionId) return;
        var playerId = ViewingPlayer(state);
        var overMap = SectorDetailLayout.TrySectorAt(_dragPoint, _cursor, out var dropSector);
        if (!overMap && SectorDetailLayout.Workspace.Contains(_dragPoint)) dropSector = _cursor;
        if (overMap)
        {
            var marker = SectorDetailLayout.Marker(_cursor, dropSector);
            if (marker is { } destination && _uiKeyedSprites is not null)
            {
                var friendlyGangs = state.FindPlayer(playerId)!.Gangs.Where(
                    gang => gang.IsActive && gang.SectorId == dropSector).ToArray();
                var source = friendlyGangs.Length == 0
                    ? OriginalSpriteLayout.IncomingGangStatus
                    : GangStatusSource(
                        state, playerId, dropSector, friendlyGangs, hasPendingHire: true);
                batch.Draw(_uiKeyedSprites, destination, source, Color.White);
            }
            for (var column = 0; column < SectorDetailLayout.Columns; column++)
            for (var row = 0; row < SectorDetailLayout.Rows; row++)
                if (SectorDetailLayout.SectorAt(_cursor, column, row) == dropSector)
                    DrawBorder(batch, pixel, SectorDetailLayout.Cell(column, row),
                        state.Sectors[dropSector].Owner == playerId ? Color.Lime : Color.OrangeRed, 2);
        }
        else if (SectorDetailLayout.Workspace.Contains(_dragPoint))
        {
            DrawBorder(batch, pixel, SectorDetailLayout.Workspace,
                state.Sectors[_cursor].Owner == playerId ? Color.Lime : Color.OrangeRed, 2);
        }
        if (_gangPortraits is null) return;
        var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 18, 36, 36);
        batch.Draw(_gangPortraits, token,
            OriginalSpriteLayout.GangPortrait(definitionId), Color.White);
        DrawBorder(batch, pixel, token, Color.White, 1);
    }

    private void BeginGangDrag(MatchGangState gang, Point point)
    {
        _draggedGangId = gang.Id;
        _gangPressPoint = point;
        _gangDragStarted = false;
        _gangDragProjection = null;
        _dragPoint = point;
    }

    /// <summary>Promotes a press into a drag, projecting up front what the drag paints with.</summary>
    private void StartGangDrag()
    {
        if (_state is null || _draggedGangId is not { } gangId
            || _state.FindGang(gangId) is not { } gang)
        {
            CancelGangDrag();
            return;
        }
        _gangDragProjection = SectorGangDragProjection.For(_state, gang, _cursor);
        _gangDragStarted = true;
    }

    private void CompleteGangClick()
    {
        var gangId = _draggedGangId;
        ForgetGangDrag();
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang) return;
        RegisterGangCardClick(gang);
    }

    private void CompleteGangDrag(Point point)
    {
        var gangId = _draggedGangId;
        ForgetGangDrag();
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang || _actions is null) return;
        var playerId = PlanningViewer ?? gang.Owner;
        var visibleGangs = SectorGangView.Visible(_state, playerId, _cursor).ToArray();
        if (SectorGangDropTarget.EnemyAt(visibleGangs, gang.Owner, point) is { } enemyId)
        {
            DropGangCommand(gang,
                new BulkCommandIntent(GangAction.Attack, CommandTarget.Gang(enemyId), Repeat: false),
                "GANG CANNOT BE ATTACKED");
            return;
        }
        // The site a double-click here would open (FND-UI-015), gaps between the portraits included.
        var siteSlot = SectorDetailLayout.SiteAt(point);
        if (siteSlot >= 0)
        {
            var target = CommandTarget.Site(_cursor * MatchLimits.SitesPerSector + siteSlot);
            var influenced = DropGangCommand(gang,
                new BulkCommandIntent(GangAction.Influence, target, Repeat: true),
                _state.Sectors[_cursor].Owner != gang.Owner
                    ? "CONTROL SECTOR TO INFLUENCE"
                    : "BUILDING CANNOT BE INFLUENCED");
            // FND-UI-018: the command handler flashes the site with fn_00419AA8 when its progress
            // is short of its resistance, that is while some resistance remains (RULE-TIMER-004).
            if (influenced && _state.Sectors[_cursor].Sites[siteSlot].Resistance != 0)
                StartFlash(TickedPresentationKind.SiteFlash, SectorDetailLayout.SitePortrait(siteSlot));
            return;
        }
        if (!SectorDetailLayout.TrySectorAt(point, _cursor, out var sectorId))
        {
            _message = string.Empty;
            return;
        }
        var intent = SectorMapGangDrop.Intent(gang.SectorId, sectorId);
        // FND-UI-018: a Move destination flashes its cell of the nine-sector display with
        // fn_0041A0D4 (RULE-TIMER-004).
        if (DropGangCommand(gang, intent, SectorMapGangDrop.Rejection(_state, gang, sectorId))
            && intent.Action == GangAction.Move
            && SectorDetailLayout.CellOf(_cursor, sectorId) is { } cell)
            StartFlash(TickedPresentationKind.SectorDisplayCellFlash, cell);
    }

    /// <summary>
    /// Carries a finished drag out: for the gang dragged alone, or for the whole ctrl-picked
    /// selection when the gang dragged is one of them.
    /// </summary>
    /// <returns>Whether at least one order was accepted.</returns>
    private bool DropGangCommand(MatchGangState gang, BulkCommandIntent intent, string rejection)
    {
        if (_state is null || _actions is null) return false;
        if (IsSelectedForBulkCommand(gang))
            return ApplyBulkCommand(gang.Owner, intent, rejection);
        var command = new GameCommand(
            gang.Owner, gang.Id, intent.Action, intent.Target, intent.Repeat);
        if (!CommandValidator.Validate(_state, command).IsValid)
        {
            RejectInput(rejection);
            return false;
        }
        var result = _actions.Submit(command);
        ReportInputResult(result.Accepted, result.Validation.Message);
        return result.Accepted;
    }

    private void CancelGangDrag()
    {
        ForgetGangDrag();
        _message = string.Empty;
    }

    /// <summary>Lets go of a drag in progress, along with what it was painting with.</summary>
    /// <remarks>
    /// Every path that replaces the match on screen or ends the turn being planned calls this, for
    /// the reason those paths already clear the ctrl-picked selection: the gang under the pointer
    /// was picked up on a turn that is over, and releasing the button would aim its order at a
    /// board the player never saw.
    /// </remarks>
    private void ForgetGangDrag()
    {
        _draggedGangId = null;
        _gangDragStarted = false;
        _gangDragProjection = null;
    }

    /// <summary>
    /// What the drag paints with, taken again when the board it was projected from has moved on.
    /// </summary>
    /// <remarks>
    /// The projection is kept for the whole gesture so its cost is paid once rather than once per
    /// frame, and the paths that replace the match or end the turn drop the drag outright. This is
    /// the guard for everything in between that they cannot see — the minimap scrolled under a
    /// held button, or a state swapped between an update and the draw that follows it — and it
    /// keeps what is painted equal to what <see cref="CompleteGangDrag"/> will act on.
    /// </remarks>
    private SectorGangDragProjection GangDragProjection(MatchState state, MatchGangState gang)
    {
        if (_gangDragProjection is { } held && held.Describes(state, gang.Id, _cursor)) return held;
        return _gangDragProjection = SectorGangDragProjection.For(state, gang, _cursor);
    }

    private void DrawGangMoveDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_gangDragStarted || _draggedGangId is not { } gangId
            || state.FindGang(gangId) is not { } gang)
            return;
        var projection = GangDragProjection(state, gang);
        var legalCommands = projection.LegalCommands;
        var legalSectors = projection.LegalSectors;
        var legalSites = projection.LegalSites;
        var visibleGangs = projection.VisibleGangs;
        if (SectorGangDropTarget.EnemyAt(visibleGangs, gang.Owner, _dragPoint) is { } enemyId)
        {
            var canAttack = legalCommands.Any(command => command.Action == GangAction.Attack
                && command.Target == CommandTarget.Gang(enemyId));
            var displayed = visibleGangs
                .OrderBy(candidate => candidate.Owner == gang.Owner ? 0 : 1)
                .ThenBy(candidate => candidate.Id.Value)
                .Take(SectorGangCardLayout.VisibleCards)
                .ToArray();
            var enemySlot = Array.FindIndex(displayed, candidate => candidate.Id == enemyId);
            if (enemySlot >= 0)
                DrawBorder(batch, pixel, SectorGangCardLayout.Frame(enemySlot),
                    canAttack ? Color.Lime : Color.OrangeRed, 2);
        }
        for (var column = 0; column < SectorDetailLayout.Columns; column++)
        for (var row = 0; row < SectorDetailLayout.Rows; row++)
        {
            if (SectorDetailLayout.SectorAt(_cursor, column, row) is not { } sectorId
                || !legalSectors.Contains(sectorId)) continue;
            var destination = SectorDetailLayout.Cell(column, row);
            destination.Inflate(-2, -2);
            batch.Draw(pixel, destination, GangDragDestinationWash);
            DrawBorder(batch, pixel, destination, GangDragDestinationOutline, 1);
        }
        for (var siteSlot = 0; siteSlot < MatchLimits.SitesPerSector; siteSlot++)
        {
            var siteId = _cursor * MatchLimits.SitesPerSector + siteSlot;
            if (legalSites.Contains(siteId))
                DrawBorder(batch, pixel, SectorDetailLayout.SitePortrait(siteSlot),
                    GangDragSectorHighlight, 1);
        }
        if (_gangPortraits is null) return;
        var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 14, 36, 28);
        batch.Draw(_gangPortraits, token, OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, token,
            IsSelectedForBulkCommand(gang) ? Color.Gold : PlayerColors[gang.Owner.Value], 1);
    }

    private void DrawSectorGangCard(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        PlayerId viewer,
        MatchGangState gang,
        int slot)
    {
        var frame = SectorGangCardLayout.Frame(slot);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, frame, OriginalSpriteLayout.GangCardFrame, Color.White);
        else
        {
            batch.Draw(pixel, frame, new Color(115, 115, 115));
            batch.Draw(pixel, new Rectangle(frame.X + 3, frame.Y + 3, frame.Width - 6, frame.Height - 7), Color.Black);
        }
        DrawBorder(batch, pixel, SectorGangCardLayout.OwnerBorder(slot),
            PlayerColors[gang.Owner.Value], 1);
        if (gang.Owner == viewer && _gangSelection.Contains(gang.Id))
            DrawBorder(batch, pixel, SectorGangCardLayout.SelectionBorder(slot), Color.Gold, 2);

        var force = SectorGangCardLayout.ForceBar(slot);
        DrawSectorMeter(batch, pixel, force, SectorGangCardLayout.ForceWidth(gang.Force),
            new Color(0, 247, 0));

        var action = gang.QueuedCommand?.Command.Action ?? GangAction.None;
        if (_uiSprites is not null && gang.Owner == viewer)
        {
            batch.Draw(_uiSprites, SectorGangCardLayout.ActionStrip(slot),
                OriginalSpriteLayout.GangActionStrip(action), Color.White);
        }
        else if (_uiSprites is null)
        {
            var strip = SectorGangCardLayout.ActionStrip(slot);
            batch.Draw(pixel, strip, Color.Black);
            var label = action == GangAction.None ? "V  VV" : action.ToString().ToUpperInvariant();
            font.Draw(batch, label, new Vector2(strip.X + 2, strip.Y + 1), Color.Lime, 1);
        }

        var definition = state.Definitions.Gang(gang.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, SectorGangCardLayout.Portrait(slot),
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        var equippedItems = EquippedItems(gang);
        for (var itemSlot = 0; itemSlot < 3; itemSlot++)
        {
            if (_itemPortraits is not null && equippedItems[itemSlot] is { } itemId)
                batch.Draw(_itemPortraits, SectorGangCardLayout.ItemPortrait(slot, itemSlot),
                    OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
        }
    }

    private static void DrawSectorMeter(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle track,
        int filledWidth,
        Color fill)
    {
        if (track.Height != 3) throw new ArgumentException("A native meter must be three pixels high.", nameof(track));
        DrawMeterRows(batch, pixel, track,
            new Color(255, 148, 148), new Color(247, 0, 0), new Color(148, 0, 0));
        var width = Math.Clamp(filledWidth, 0, track.Width);
        if (width == 0) return;
        var filled = new Rectangle(track.X, track.Y, width, track.Height);
        if (fill == new Color(190, 0, 220))
            DrawMeterRows(batch, pixel, filled,
                new Color(255, 148, 255), fill, new Color(108, 0, 125));
        else
            DrawMeterRows(batch, pixel, filled,
                new Color(148, 255, 148), new Color(0, 247, 0), new Color(0, 140, 0));
    }

    private static void DrawMeterRows(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bounds,
        Color highlight,
        Color center,
        Color shadow)
    {
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), highlight);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + 1, bounds.Width, 1), center);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, 1), shadow);
    }

    private static void DrawHorizontalArrow(
        SpriteBatch batch,
        Texture2D pixel,
        Rectangle bounds,
        bool left,
        Color color)
    {
        for (var offset = 0; offset < 7; offset++)
        {
            var height = 1 + offset * 2;
            var x = left ? bounds.X + 2 + offset : bounds.Right - 3 - offset;
            batch.Draw(pixel, new Rectangle(x, bounds.Center.Y - height / 2, 1, height), color);
        }
    }

    /// <summary>
    /// FND-UI-018: the nine-sector display, a crop of the prepared city map with the off-map
    /// cells blacked out, under the keyed frame and the labels of FND-UI-038.
    /// </summary>
    private void DrawSectorNeighborhood(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        var display = SectorDetailLayout.Display;
        DrawPreparedCityMap(batch, pixel, state, ViewingPlayer(state),
            SectorDetailLayout.DisplaySource(_cursor), display.Location);
        foreach (var band in SectorDetailLayout.OffMapBands(_cursor))
            batch.Draw(pixel, band, Color.Black);
        // FND-UI-037: the cell flash lightens the copied display, markers included, and the frame
        // and edge labels are drawn over it unlit.
        DrawFlashLightening(batch, TickedPresentationKind.SectorDisplayCellFlash);
        if (_uiKeyedSprites is not null)
        {
            batch.Draw(_uiKeyedSprites, display, SectorDetailLayout.DisplayFrameSource, Color.White);
            // FND-UI-048: the pump keys the selection frame over the centre cell, covering the
            // outline the display's frame draws there.
            batch.Draw(_uiKeyedSprites, SectorDetailLayout.DisplayCentre,
                CityMapLayout.SelectionFrameSource(SelectionFrameShown()), Color.White);
        }
        foreach (var label in SectorDetailLayout.DisplayLabels(_cursor))
            DrawGridLabel(batch, font, label);
    }

    private void DrawGangStatusMarker(SpriteBatch batch, Rectangle destination, Rectangle source)
    {
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, destination, source, Color.White);
    }
}

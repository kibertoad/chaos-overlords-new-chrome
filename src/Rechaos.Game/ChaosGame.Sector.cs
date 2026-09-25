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
        if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
            _screens.Show(ClientScreen.City);
    }

    /// <summary>
    /// The gangs the Sector workspace lists. A borrowed opponent roster falls back to the viewer's
    /// own gangs once the opponent no longer keeps a detectable gang in the selected sector.
    /// </summary>
    /// <summary>
    /// SCR-UI-004, FND-UI-018: the group order strip is drawn when the cards show at least two
    /// gangs of the player whose turn it is, during planning.
    /// </summary>
    private bool ShowsGroupOrderStrip(MatchState state, PlayerId viewer) =>
        state.Coordinator.Phase == TurnPhase.Command
        && state.Coordinator.ActivePlayer == viewer
        && SectorCardGangs(state, viewer) is { Count: >= 2 } cards
        && cards[0].Owner == viewer;

    private IReadOnlyList<MatchGangState> SectorCardGangs(MatchState state, PlayerId viewer)
    {
        if (_sectorGangCardOwner is { } owner && owner != viewer
            && SectorOpponentGangs.InSector(state, viewer, owner, _cursor) is { Count: > 0 } borrowed)
            return borrowed;
        return SectorOpponentGangs.InSector(state, viewer, viewer, _cursor);
    }

    /// <summary>
    /// Points the workspace at an overlord's gangs. RULE-UI-010: a portrait switches the cards
    /// only when the viewer can see a gang of that overlord in the sector, and the viewer's own
    /// portrait then restores the viewer's roster. The portrait of an overlord with no such gang
    /// leaves the cards as they are.
    /// </summary>
    private void SelectSectorGangCardOwner(MatchState state, PlayerId viewer, PlayerId owner)
    {
        _message = string.Empty;
        if (owner == viewer)
        {
            if (SectorOpponentGangs.InSector(state, viewer, viewer, _cursor).Count > 0)
                _sectorGangCardOwner = null;
            return;
        }
        if (!SectorOpponentGangs.Detectable(state, viewer, owner, _cursor)) return;
        _sectorGangCardOwner = owner;
        // Borrowing an opponent's cards puts the player's own gangs out of sight, and a pick
        // nobody can see is a pick nobody meant to keep.
        _gangSelection.Clear();
    }

    private void HandleSectorClick(Point point)
    {
        if (_idleGangWarningOpen)
        {
            HandleIdleGangWarningClick(point);
            return;
        }
        if (SectorDetailLayout.Back.Contains(point) || ManagementBack.Contains(point))
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        if (_state is null) return;
        var playerId = ViewingPlayer(_state);
        if (SectorOpponentGangs.PortraitAt(_state, point) is { } portraitOwner)
        {
            SelectSectorGangCardOwner(_state, playerId, portraitOwner);
            return;
        }
        if (BeginCityConsolePress(point, ClientScreen.Sector)) return;
        var rejectSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.Reject, point);
        var hireSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.PortraitHit, point);
        if (rejectSlot >= 0)
        {
            BeginHireReject(rejectSlot, ClientScreen.Sector);
            return;
        }
        if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
            return;
        }
        if (SectorDetailLayout.TrySectorAt(point, _cursor, out var selectedSector))
        {
            if (selectedSector != _cursor) _sectorGangCardOwner = null;
            _cursor = selectedSector;
            _gangSelection.KeepOnly(_cursor);
            _message = string.Empty;
            return;
        }
        var siteSlot = HitTest.IndexAt(MatchLimits.SitesPerSector, SectorDetailLayout.SitePortrait, point);
        if (siteSlot >= 0)
        {
            if (_sectorSiteClicks.Register(_cursor * MatchLimits.SitesPerSector + siteSlot, _inputTime))
                OpenSiteDetails(_cursor, siteSlot, ClientScreen.Sector);
            return;
        }
        if (SectorDetailLayout.GroupOrderStrip.Contains(point))
        {
            if (ShowsGroupOrderStrip(_state, playerId))
                OpenGroupCommands(_state, playerId,
                    SectorDetailLayout.GroupOrderIsRecurring(point));
            return;
        }
        var visible = SectorCardGangs(_state, playerId)
            .Take(SectorGangCardLayout.VisibleCards).ToArray();
        var index = HitTest.IndexAt(visible.Length, SectorGangCardLayout.Frame, point);
        if (index < 0) return;
        var gang = visible[index];
        if (gang.Owner != playerId)
        {
            _message = string.Empty;
            if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
                OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
            return;
        }
        var ownGangs = _state.FindPlayer(playerId)!.Gangs.Where(candidate => candidate.IsActive).ToArray();
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
        else if (SectorGangCardLayout.Portrait(index).Contains(point))
            BeginGangDrag(gang, point);
        else if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
    }

    private void DrawSectorDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawBoard(batch, pixel, font, state, drawMapLayer: false);
        batch.Draw(pixel, new Rectangle(0, 42, 438, 418), Color.Black);
        DrawSectorSideRail(batch, pixel, font);
        var sector = state.Sectors[_cursor];
        var viewer = ViewingPlayer(state);
        DrawSectorOpponentGangPresence(batch, pixel, font, state, viewer);
        DrawSectorNeighborhood(batch, pixel, font, state);
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Site(site.DefinitionId);
            var portrait = SectorDetailLayout.SitePortrait(site.Slot);
            var controlOwner = SiteControlRules.Controller(sector, site);
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, portrait,
                    OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
            DrawBorder(batch, pixel, portrait,
                controlOwner is { } influencedBy ? PlayerColors[influencedBy.Value] : Color.Gray, 1);
            var control = SectorDetailLayout.SiteControlBar(site.Slot);
            var controlled = SectorDetailLayout.SiteControlWidth(
                definition.Resistance, site.Resistance);
            DrawSectorMeter(batch, pixel, control, controlled,
                SectorDetailLayout.SiteControlColor(controlOwner, viewer));
        }
        var visibleGangs = SectorCardGangs(state, viewer);
        foreach (var entry in visibleGangs.Take(SectorGangCardLayout.VisibleCards)
                     .Select((gang, index) => (gang, index)))
            DrawSectorGangCard(batch, pixel, font, state, viewer, entry.gang, entry.index);
        if (visibleGangs.Count > SectorGangCardLayout.VisibleCards)
            font.Draw(batch, $"+{visibleGangs.Count - SectorGangCardLayout.VisibleCards}",
                new Vector2(397, 123), Color.White, 1);
        if (_uiSprites is not null && ShowsGroupOrderStrip(state, viewer))
            batch.Draw(_uiSprites, SectorDetailLayout.GroupOrderStrip,
                OriginalSpriteLayout.GroupOrderStrip, Color.White);
        DrawQueuedCommandTargetHighlight(batch, pixel, viewer, visibleGangs);
        DrawGangMoveDrag(batch, pixel, state);
        DrawSectorHireDrag(batch, pixel, state);
        // The Sector workspace covers the left side of right-edge tooltips drawn by
        // DrawBoard, so composite the tooltip again after the workspace is complete.
        DrawStatusConsoleTooltip(batch, pixel, font);
        // RULE-TURN-005: what the group order strip does, as FND-TURN-009 records it.
        if (_hoverPoint is { } stripHover && ShowsGroupOrderStrip(state, viewer)
            && SectorDetailLayout.GroupOrderStrip.Contains(stripHover))
            DrawHoverTooltip(batch, pixel, font, stripHover,
            [
                "GROUP ORDER",
                "GIVES ONE ORDER TO EVERY GANG YOU HAVE HERE, HIDING OR NOT.",
                "LEFT HALF: FOR THIS TURN. RIGHT HALF: RECURRING,",
                "WITHOUT RESEARCH. HEAL SKIPS GANGS AT FORCE 10.",
                "THE ORDER REPLACES EACH GANG'S PREVIOUS ONE."
            ]);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
    }

    /// <summary>
    /// Flags every opponent holding gangs the viewer can see in the selected sector, and marks the
    /// one whose gangs the cards are currently listing.
    /// </summary>
    private void DrawSectorOpponentGangPresence(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        PlayerId viewer)
    {
        foreach (var opponent in state.Setup.Players)
        {
            if (!SectorOpponentGangs.Detectable(state, viewer, opponent.Id, _cursor)) continue;
            var banner = PlayerPortraitLayout.CityGangPresence(opponent.Id.Value);
            batch.Draw(pixel, banner, Color.Black);
            font.Draw(batch, PlayerPortraitLayout.GangPresenceLabel,
                new Vector2(banner.X, banner.Y), new Color(247, 0, 0), 1);
            if (_sectorGangCardOwner == opponent.Id)
                DrawBorder(batch, pixel, PlayerPortraitLayout.CityTop(opponent.Id.Value),
                    new Color(247, 0, 0), 1);
        }
    }

    private void DrawQueuedCommandTargetHighlight(
        SpriteBatch batch,
        Texture2D pixel,
        PlayerId viewer,
        IReadOnlyList<MatchGangState> visibleGangs)
    {
        if (_hoverPoint is not { } point) return;
        var hoveredSlot = HitTest.IndexAt(
            Math.Min(visibleGangs.Count, SectorGangCardLayout.VisibleCards),
            SectorGangCardLayout.Frame,
            point);
        // An opponent's orders stay their own business: the cards hide their action strip, so the
        // workspace must not betray the same order by highlighting what it targets.
        if (hoveredSlot < 0 || visibleGangs[hoveredSlot].Owner != viewer
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
        if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
    }

    private void CompleteGangDrag(Point point)
    {
        var gangId = _draggedGangId;
        ForgetGangDrag();
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang || _actions is null) return;
        var playerId = _state.Coordinator.ActivePlayer ?? gang.Owner;
        var visibleGangs = SectorGangView.Visible(_state, playerId, _cursor).ToArray();
        if (SectorGangDropTarget.EnemyAt(visibleGangs, gang.Owner, point) is { } enemyId)
        {
            DropGangCommand(gang,
                new BulkCommandIntent(GangAction.Attack, CommandTarget.Gang(enemyId), Repeat: false),
                "GANG CANNOT BE ATTACKED");
            return;
        }
        var siteSlot = HitTest.IndexAt(MatchLimits.SitesPerSector, SectorDetailLayout.SitePortrait, point);
        if (siteSlot >= 0)
        {
            var target = CommandTarget.Site(_cursor * MatchLimits.SitesPerSector + siteSlot);
            DropGangCommand(gang,
                new BulkCommandIntent(GangAction.Influence, target, Repeat: true),
                _state.Sectors[_cursor].Owner != gang.Owner
                    ? "CONTROL SECTOR TO INFLUENCE"
                    : "BUILDING CANNOT BE INFLUENCED");
            return;
        }
        if (!SectorDetailLayout.TrySectorAt(point, _cursor, out var sectorId))
        {
            _message = string.Empty;
            return;
        }
        DropGangCommand(gang, SectorMapGangDrop.Intent(gang.SectorId, sectorId),
            SectorMapGangDrop.Rejection(_state, gang, sectorId));
    }

    /// <summary>
    /// Carries a finished drag out: for the gang dragged alone, or for the whole ctrl-picked
    /// selection when the gang dragged is one of them.
    /// </summary>
    private void DropGangCommand(MatchGangState gang, BulkCommandIntent intent, string rejection)
    {
        if (_state is null || _actions is null) return;
        if (IsSelectedForBulkCommand(gang))
        {
            ApplyBulkCommand(gang.Owner, intent, rejection);
            return;
        }
        var command = new GameCommand(
            gang.Owner, gang.Id, intent.Action, intent.Target, intent.Repeat);
        if (!CommandValidator.Validate(_state, command).IsValid)
        {
            RejectInput(rejection);
            return;
        }
        var result = _actions.Submit(command);
        ReportInputResult(result.Accepted, result.Validation.Message);
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

    private void DrawSectorSideRail(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var rail = new Rectangle(3, 43, 29, 417);
        batch.Draw(pixel, rail, new Color(105, 105, 105));
        DrawBorder(batch, pixel, rail, new Color(185, 185, 185), 1);
        batch.Draw(pixel, new Rectangle(6, 46, 22, 99), new Color(0, 180, 20));
        batch.Draw(pixel, new Rectangle(8, 48, 18, 95), new Color(210, 0, 0));
        batch.Draw(pixel, new Rectangle(6, 146, 22, 248), Color.Black);
        for (var y = 150; y < 394; y += 8)
            batch.Draw(pixel, new Rectangle(7, y, 20, 1), new Color(0, 20, 115));
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, SectorDetailLayout.Back,
                OriginalSpriteLayout.SectorBackArrow, Color.White);
        else
            font.Draw(batch, "<", new Vector2(8, 410), Color.Lime, 2);
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

    private void DrawSectorNeighborhood(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        for (var row = 0; row < SectorDetailLayout.Rows; row++)
        for (var column = 0; column < SectorDetailLayout.Columns; column++)
        {
            var destination = SectorDetailLayout.Cell(column, row);
            if (SectorDetailLayout.SectorAt(_cursor, column, row) is not { } sectorId)
            {
                batch.Draw(pixel, destination, Color.Black);
                DrawBorder(batch, pixel, destination, new Color(0, 110, 30), 1);
                continue;
            }
            var sector = state.Sectors[sectorId];
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (layer is not null)
                batch.Draw(layer, destination, CityMapLayout.Source(sectorId), Color.White);
            else
                batch.Draw(pixel, destination, new Color(24, 37, 39));
            if (_uiKeyedSprites is not null
                && ObjectiveSectorMarkerPresentation.IsMarked(
                    state.Setup.Scenario, sectorId, sector.IsImportant))
                batch.Draw(_uiKeyedSprites, destination,
                    OriginalSpriteLayout.ObjectiveSectorPylons, Color.White);
            DrawBorder(batch, pixel, destination,
                column == 1 && row == 1 ? Color.White : new Color(0, 150, 45),
                column == 1 && row == 1 ? 2 : 1);
            if (row == 0)
                DrawSectorCoordinateBadge(batch, pixel, font,
                    new Point(destination.Center.X, destination.Y + 1),
                    ((char)('A' + sectorId % 8)).ToString(), top: true);
            if (column == 0)
                DrawSectorCoordinateBadge(batch, pixel, font,
                    new Point(destination.X + 1, destination.Center.Y),
                    (sectorId / 8 + 1).ToString(), top: false);
        }

        var playerId = ViewingPlayer(state);
        // RULE-UI-006: the 3-by-3 display is copied from the prepared city map, so it shows the
        // markers the full map draw left behind.
        var markerFrames = GangStatusMarkerPresentation.MapFrames(state, playerId);
        for (var sectorId = 0; sectorId < markerFrames.Length; sectorId++)
            if (markerFrames[sectorId] >= 0
                && SectorDetailLayout.Marker(_cursor, sectorId) is { } marker)
                DrawGangStatusMarker(batch, marker, OriginalSpriteLayout.GangStatus(markerFrames[sectorId]));
    }

    private void DrawGangStatusMarker(SpriteBatch batch, Rectangle destination, Rectangle source)
    {
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, destination, source, Color.White);
    }

    private static void DrawSectorCoordinateBadge(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Point anchor,
        string label,
        bool top)
    {
        var bounds = top
            ? new Rectangle(anchor.X - 8, anchor.Y - 2, 16, 15)
            : new Rectangle(anchor.X - 3, anchor.Y - 8, 15, 16);
        batch.Draw(pixel, bounds, new Color(105, 105, 105));
        batch.Draw(pixel, new Rectangle(bounds.X + 2, bounds.Y + 1, bounds.Width - 4, bounds.Height - 3),
            Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 2, 2, 2), Color.Black);
        batch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Bottom - 2, 2, 2), Color.Black);
        font.Draw(batch, label, new Vector2(bounds.X + 5, bounds.Y + 4), Color.Lime, 1);
    }
}

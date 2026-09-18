using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void UpdateSector(KeyboardState keyboard)
    {
        if (_idleGangWarningOpen)
        {
            UpdateIdleGangWarning(keyboard);
            return;
        }
        var column = _cursor % 8;
        var row = _cursor / 8;
        if (Pressed(keyboard, Keys.Left) && column > 0) _cursor--;
        if (Pressed(keyboard, Keys.Right) && column < 7) _cursor++;
        if (Pressed(keyboard, Keys.Up) && row > 0) _cursor -= 8;
        if (Pressed(keyboard, Keys.Down) && row < 7) _cursor += 8;
        if (Pressed(keyboard, Keys.Back) || Pressed(keyboard, Keys.Enter))
            _screens.Show(ClientScreen.City);
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
        if (BeginCityConsolePress(point, ClientScreen.Sector)) return;
        var rejectSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Reject(slot).Contains(point), -1);
        var hireSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Portrait(slot).Contains(point), -1);
        if (rejectSlot >= 0)
        {
            SnubHireDockOffer(rejectSlot, pointerButton: true);
            return;
        }
        if (hireSlot >= 0)
        {
            BeginHireDrag(hireSlot, point);
            return;
        }
        if (SectorDetailLayout.TrySectorAt(point, _cursor, out var selectedSector))
        {
            _cursor = selectedSector;
            _message = string.Empty;
            return;
        }
        var siteSlot = Enumerable.Range(0, MatchLimits.SitesPerSector)
            .FirstOrDefault(slot => SectorDetailLayout.SitePortrait(slot).Contains(point), -1);
        if (siteSlot >= 0)
        {
            if (_sectorSiteClicks.Register(_cursor * MatchLimits.SitesPerSector + siteSlot, _inputTime))
                OpenSiteDetails(_cursor, siteSlot, ClientScreen.Sector);
            return;
        }
        var playerId = _state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var visible = _state.FindPlayer(playerId)!.Gangs
            .Where(gang => gang.IsActive && gang.SectorId == _cursor)
            .OrderBy(gang => gang.Id.Value)
            .Take(SectorGangCardLayout.VisibleCards).ToArray();
        var index = Enumerable.Range(0, visible.Length)
            .FirstOrDefault(value => SectorGangCardLayout.Frame(value).Contains(point), -1);
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
        var repeat = SectorGangCardLayout.ActionRepeatAt(index, point);
        if (repeat is { } selectedRepeat)
            OpenCommands(selectedRepeat, ClientScreen.Sector);
        else if (SectorGangCardLayout.Portrait(index).Contains(point))
            BeginGangDrag(gang, point);
        else if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
    }

    private void DrawSectorDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        batch.Draw(pixel, new Rectangle(0, 42, 438, 418), Color.Black);
        DrawSectorSideRail(batch, pixel, font);
        var sector = state.Sectors[_cursor];
        var viewer = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        DrawSectorNeighborhood(batch, pixel, font, state);
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
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
        var visibleGangs = state.FindPlayer(viewer)!.Gangs
            .Where(gang => gang.IsActive && gang.SectorId == sector.Id)
            .OrderBy(gang => gang.Id.Value)
            .ToArray();
        foreach (var entry in visibleGangs.Take(SectorGangCardLayout.VisibleCards)
                     .Select((gang, index) => (gang, index)))
            DrawSectorGangCard(batch, pixel, font, state, viewer, entry.gang, entry.index);
        if (visibleGangs.Length > SectorGangCardLayout.VisibleCards)
            font.Draw(batch, $"+{visibleGangs.Length - SectorGangCardLayout.VisibleCards}",
                new Vector2(397, 123), Color.White, 1);
        DrawQueuedCommandTargetHighlight(batch, pixel, visibleGangs);
        DrawGangMoveDrag(batch, pixel, state);
        DrawSectorHireDrag(batch, pixel, state);
        // The Sector workspace covers the left side of right-edge tooltips drawn by
        // DrawBoard, so composite the tooltip again after the workspace is complete.
        DrawStatusConsoleTooltip(batch, pixel, font);
        if (_idleGangWarningOpen) DrawIdleGangWarning(batch, pixel, font);
    }

    private void DrawQueuedCommandTargetHighlight(
        SpriteBatch batch,
        Texture2D pixel,
        IReadOnlyList<MatchGangState> visibleGangs)
    {
        if (_hoverPoint is not { } point) return;
        var hoveredSlot = Enumerable.Range(0,
                Math.Min(visibleGangs.Count, SectorGangCardLayout.VisibleCards))
            .FirstOrDefault(slot => SectorGangCardLayout.Frame(slot).Contains(point), -1);
        if (hoveredSlot < 0 || visibleGangs[hoveredSlot].QueuedCommand is not { } queued) return;

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
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
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
        _dragPoint = point;
    }

    private void CompleteGangClick()
    {
        var gangId = _draggedGangId;
        _draggedGangId = null;
        _gangDragStarted = false;
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang) return;
        if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector, gang.SectorId);
    }

    private void CompleteGangDrag(Point point)
    {
        var gangId = _draggedGangId;
        _draggedGangId = null;
        _gangDragStarted = false;
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang || _actions is null) return;
        var playerId = _state.Coordinator.ActivePlayer ?? gang.Owner;
        var visibleGangs = SectorGangView.Visible(_state, playerId, _cursor).ToArray();
        if (SectorGangDropTarget.EnemyAt(visibleGangs, gang.Owner, point) is { } enemyId)
        {
            var attackTarget = CommandTarget.Gang(enemyId);
            var attack = CommandOptionCatalog.LegalCommands(_state, gang.Owner, gang.Id)
                .FirstOrDefault(command => command.Action == GangAction.Attack
                    && command.Target == attackTarget);
            if (attack is null)
            {
                RejectInput("GANG CANNOT BE ATTACKED");
                return;
            }
            var attackResult = _actions.Submit(attack with { Repeat = false });
            ReportInputResult(attackResult.Accepted, attackResult.Validation.Message);
            return;
        }
        var siteSlot = Enumerable.Range(0, MatchLimits.SitesPerSector)
            .FirstOrDefault(slot => SectorDetailLayout.SitePortrait(slot).Contains(point), -1);
        if (siteSlot >= 0)
        {
            var target = CommandTarget.Site(_cursor * MatchLimits.SitesPerSector + siteSlot);
            var influence = CommandOptionCatalog.LegalCommands(_state, gang.Owner, gang.Id)
                .FirstOrDefault(command => command.Action == GangAction.Influence
                    && command.Target == target);
            if (influence is null)
            {
                RejectInput(_state.Sectors[_cursor].Owner != gang.Owner
                    ? "CONTROL SECTOR TO INFLUENCE"
                    : "BUILDING CANNOT BE INFLUENCED");
                return;
            }
            var influenceResult = _actions.Submit(influence with { Repeat = true });
            ReportInputResult(influenceResult.Accepted, influenceResult.Validation.Message);
            return;
        }
        if (!SectorDetailLayout.TrySectorAt(point, _cursor, out var sectorId))
        {
            _message = string.Empty;
            return;
        }
        var legalCommands = CommandOptionCatalog.LegalCommands(_state, gang.Owner, gang.Id);
        if (SectorMapGangDrop.Resolve(legalCommands, gang.SectorId, sectorId) is not { } dropped)
        {
            RejectInput(SectorMapGangDrop.Rejection(_state, gang, sectorId));
            return;
        }
        var result = _actions.Submit(dropped);
        ReportInputResult(result.Accepted, result.Validation.Message);
    }

    private void CancelGangDrag()
    {
        _draggedGangId = null;
        _gangDragStarted = false;
        _message = string.Empty;
    }

    private void DrawGangMoveDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_gangDragStarted || _draggedGangId is not { } gangId || state.FindGang(gangId) is not { } gang)
            return;
        var legalCommands = CommandOptionCatalog.LegalCommands(state, gang.Owner, gang.Id);
        var legalSectors = SectorMapGangDrop.Destinations(legalCommands, gang.SectorId);
        var visibleGangs = SectorGangView.Visible(state, gang.Owner, _cursor).ToArray();
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
            DrawBorder(batch, pixel, SectorDetailLayout.Cell(column, row), Color.Lime, 2);
        }
        if (_gangPortraits is null) return;
        var token = new Rectangle(_dragPoint.X - 18, _dragPoint.Y - 14, 36, 28);
        batch.Draw(_gangPortraits, token, OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, token, PlayerColors[gang.Owner.Value], 1);
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

        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, SectorGangCardLayout.Portrait(slot),
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        short?[] equippedItems =
            [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
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

        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var activeGangsBySector = player.Gangs.Where(gang => gang.IsActive)
            .GroupBy(gang => gang.SectorId).ToArray();
        var pendingHireSectors = player.PendingHires
            .Select(pending => pending.TargetSectorId).ToHashSet();
        foreach (var gangs in activeGangsBySector)
            if (SectorDetailLayout.Marker(_cursor, gangs.Key) is { } gangMarker)
                DrawGangStatusMarker(batch, gangMarker,
                    GangStatusSource(state, playerId, gangs.Key, gangs,
                        pendingHireSectors.Contains(gangs.Key)));
        var occupiedGangSectors = activeGangsBySector.Select(gangs => gangs.Key).ToHashSet();
        foreach (var pendingSector in pendingHireSectors.Where(
                     pendingSector => !occupiedGangSectors.Contains(pendingSector)))
            if (SectorDetailLayout.Marker(_cursor, pendingSector) is { } hireMarker)
                DrawGangStatusMarker(batch, hireMarker, OriginalSpriteLayout.IncomingGangStatus);
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

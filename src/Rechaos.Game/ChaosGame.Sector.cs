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
        if (SectorDetailLayout.Back.Contains(point) || ManagementBack.Contains(point))
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        if (_state is null) return;
        if (HandleCityConsoleClick(point, ClientScreen.Sector)) return;
        var rejectSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Reject(slot).Contains(point), -1);
        var hireSlot = Enumerable.Range(0, HireDockLayout.SlotCount)
            .FirstOrDefault(slot => HireDockLayout.Portrait(slot).Contains(point), -1);
        if (rejectSlot >= 0)
        {
            SnubHireDockOffer(rejectSlot);
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
            _message = $"SECTOR {_cursor + 1}";
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
        var visible = SectorGangView.Visible(_state, playerId, _cursor)
            .OrderBy(gang => gang.Owner == playerId ? 0 : 1)
            .ThenBy(gang => gang.Id.Value)
            .Take(SectorGangCardLayout.VisibleCards).ToArray();
        var index = Enumerable.Range(0, visible.Length)
            .FirstOrDefault(value => SectorGangCardLayout.Frame(value).Contains(point), -1);
        if (index < 0) return;
        var gang = visible[index];
        if (gang.Owner != playerId)
        {
            _message = "ENEMY GANG DETECTED";
            return;
        }
        var ownGangs = _state.FindPlayer(playerId)!.Gangs.Where(candidate => candidate.IsActive).ToArray();
        _selectedGangIndex = Array.FindIndex(ownGangs, candidate => candidate.Id == gang.Id);
        if (gang.QueuedCommand is { } queued
            && SectorGangCardLayout.AssignedCommand(index).Contains(point))
            OpenCommands(queued.Command.Repeat, ClientScreen.Sector);
        else if (SectorGangCardLayout.OneOffAction(index).Contains(point))
            OpenCommands(repeat: false, returnScreen: ClientScreen.Sector);
        else if (SectorGangCardLayout.RepeatingAction(index).Contains(point))
            OpenCommands(repeat: true, returnScreen: ClientScreen.Sector);
        else if (SectorGangCardLayout.Portrait(index).Contains(point))
            BeginGangDrag(gang, point);
        else if (_sectorGangClicks.Register(gang.Id.Value, _inputTime))
            OpenGangDetails(gang, ClientScreen.Sector);
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
            if (_sitePortraits is not null)
                batch.Draw(_sitePortraits, portrait,
                    OriginalSpriteLayout.SitePortrait(definition.Id), Color.White);
            DrawBorder(batch, pixel, portrait,
                site.InfluencedBy is { } influencedBy ? PlayerColors[influencedBy.Value] : Color.Gray, 1);
            var control = SectorDetailLayout.SiteControlBar(site.Slot);
            batch.Draw(pixel, control, Color.Red);
            var controlled = definition.Resistance == 0
                ? control.Width
                : (int)Math.Round(control.Width
                    * (definition.Resistance - Math.Clamp(site.Resistance, 0, definition.Resistance))
                    / (double)definition.Resistance);
            if (controlled > 0)
                batch.Draw(pixel, new Rectangle(control.X, control.Y, controlled, control.Height),
                    SectorDetailLayout.SiteControlColor(site.InfluencedBy, viewer));
        }
        var visibleGangs = SectorGangView.Visible(state, viewer, sector.Id)
            .OrderBy(gang => gang.Owner == viewer ? 0 : 1)
            .ThenBy(gang => gang.Id.Value)
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
            if (marker is { } destination && _uiSprites is not null)
                batch.Draw(_uiSprites, destination, OriginalSpriteLayout.IncomingGangStatus, Color.White);
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
            OpenGangDetails(gang, ClientScreen.Sector);
    }

    private void CompleteGangDrag(Point point)
    {
        var gangId = _draggedGangId;
        _draggedGangId = null;
        _gangDragStarted = false;
        if (gangId is null || _state?.FindGang(gangId.Value) is not { } gang || _replay is null) return;
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
                _message = "BUILDING CANNOT BE INFLUENCED";
                return;
            }
            var influenceResult = _replay.Submit(influence with { Repeat = true });
            _message = influenceResult.Accepted
                ? $"INFLUENCE SITE {siteSlot + 1} QUEUED"
                : influenceResult.Validation.Message.ToUpperInvariant();
            return;
        }
        if (!SectorDetailLayout.TrySectorAt(point, _cursor, out var sectorId))
        {
            _message = "MOVE CANCELLED";
            return;
        }
        var legal = CommandOptionCatalog.LegalCommands(_state, gang.Owner, gang.Id)
            .FirstOrDefault(command => command.Action == GangAction.Move
                && command.Target == CommandTarget.Sector(sectorId));
        if (legal is null)
        {
            _message = "MOVE REQUIRES A NEIGHBORING SECTOR";
            return;
        }
        var result = _replay.Submit(legal with { Repeat = false });
        _message = result.Accepted
            ? $"MOVE TO SECTOR {sectorId + 1} QUEUED"
            : result.Validation.Message.ToUpperInvariant();
    }

    private void CancelGangDrag()
    {
        _draggedGangId = null;
        _gangDragStarted = false;
        _message = "MOVE CANCELLED";
    }

    private void DrawGangMoveDrag(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        if (!_gangDragStarted || _draggedGangId is not { } gangId || state.FindGang(gangId) is not { } gang)
            return;
        var legalSectors = CommandOptionCatalog.LegalCommands(state, gang.Owner, gang.Id)
            .Where(command => command.Action == GangAction.Move)
            .Select(command => command.Target.Id)
            .ToHashSet();
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
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, SectorDetailLayout.Back,
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
        DrawBorder(batch, pixel, frame, PlayerColors[gang.Owner.Value], 2);

        var force = SectorGangCardLayout.ForceBar(slot);
        batch.Draw(pixel, force, Color.Red);
        var forceWidth = Math.Clamp((force.Width * gang.Force + 9) / 10, 0, force.Width);
        if (forceWidth > 0)
            batch.Draw(pixel, new Rectangle(force.X, force.Y, forceWidth, force.Height),
                Color.Lime);

        var controlsEnabled = gang.Owner == viewer;
        if (gang.QueuedCommand is { } queued)
        {
            var assigned = SectorGangCardLayout.AssignedCommand(slot);
            batch.Draw(pixel, assigned, new Color(20, 28, 25));
            DrawBorder(batch, pixel, assigned,
                controlsEnabled ? PlayerColors[gang.Owner.Value] : Color.Gray, 1);
            var label = queued.Command.Action.ToString().ToUpperInvariant();
            var x = assigned.Center.X - label.Length * 3;
            font.Draw(batch, label, new Vector2(x, assigned.Y + 4),
                controlsEnabled ? Color.Lime : Color.Gray, 1);
        }
        else
        {
            var once = SectorGangCardLayout.OneOffAction(slot);
            var repeat = SectorGangCardLayout.RepeatingAction(slot);
            batch.Draw(pixel, once, new Color(34, 34, 34));
            batch.Draw(pixel, repeat, new Color(34, 34, 34));
            DrawBorder(batch, pixel, once, Color.LightGray, 1);
            DrawBorder(batch, pixel, repeat, Color.LightGray, 1);
            DrawDownArrow(batch, pixel, once.Center.X, once.Y + 5,
                controlsEnabled ? Color.Lime : Color.Gray);
            DrawDownArrow(batch, pixel, repeat.Center.X, repeat.Y + 3,
                controlsEnabled ? Color.Lime : Color.Gray, compact: true);
            DrawDownArrow(batch, pixel, repeat.Center.X, repeat.Y + 9,
                controlsEnabled ? Color.Lime : Color.Gray, compact: true);
        }

        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, SectorGangCardLayout.Portrait(slot),
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        for (var itemSlot = 0; itemSlot < 3; itemSlot++)
        {
            // The source frame also contains legacy pixels in this strip. The
            // live card footer is exclusively the gang's three equipment slots.
            var item = SectorGangCardLayout.ItemSlot(slot, itemSlot);
            batch.Draw(pixel, item, Color.Black);
            DrawBorder(batch, pixel, item, Color.LightGray, 1);
        }
    }

    private static void DrawDownArrow(
        SpriteBatch batch,
        Texture2D pixel,
        int centerX,
        int top,
        Color color,
        bool compact = false)
    {
        var widths = compact ? new[] { 7, 5, 3, 1 } : new[] { 9, 7, 5, 3, 1 };
        for (var row = 0; row < widths.Length; row++)
            batch.Draw(pixel, new Rectangle(centerX - widths[row] / 2, top + row, widths[row], 1), color);
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
            if (sector.CrackdownActive && _policeSprites is not null)
                batch.Draw(_policeSprites,
                    new Rectangle(destination.X + 14, destination.Y + 10, 27, 32),
                    OriginalSpriteLayout.PolicePatrolCar, Color.White);
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
        foreach (var gang in player.Gangs.Where(gang => gang.IsActive))
            if (SectorDetailLayout.Marker(_cursor, gang.SectorId) is { } gangMarker)
                DrawGangStatusMarker(batch, gangMarker,
                    gang.QueuedCommand is null
                        ? OriginalSpriteLayout.IdleGangStatus
                        : OriginalSpriteLayout.AssignedGangStatus);
        foreach (var pending in player.PendingHires)
            if (SectorDetailLayout.Marker(_cursor, pending.TargetSectorId) is { } hireMarker)
                DrawGangStatusMarker(batch, hireMarker, OriginalSpriteLayout.IncomingGangStatus);
    }

    private void DrawGangStatusMarker(SpriteBatch batch, Rectangle destination, Rectangle source)
    {
        if (_uiSprites is not null) batch.Draw(_uiSprites, destination, source, Color.White);
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

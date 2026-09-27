using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// Draws the part <paramref name="crop"/> of the prepared city map, in map coordinates, with
    /// its corner at <paramref name="destination"/>. The map holds what FND-UI-025 has the city
    /// compositor draw into it: the neutral map, the owned interiors, the objective pylons, the
    /// site markers and the gang-status markers of <paramref name="viewer"/>. SCR-UI-003 shows all
    /// of it at <c>(2,42)</c>; SCR-UI-004's nine-sector display shows a 162-by-156 crop
    /// (FND-UI-018).
    /// </summary>
    private void DrawPreparedCityMap(
        SpriteBatch batch, Texture2D pixel, MatchState state, PlayerId viewer, Rectangle crop, Point destination)
    {
        void Draw(Texture2D texture, Rectangle source, Rectangle mapArea)
        {
            var visible = Rectangle.Intersect(mapArea, crop);
            if (visible.IsEmpty) return;
            batch.Draw(texture,
                new Rectangle(destination.X + visible.X - crop.X, destination.Y + visible.Y - crop.Y,
                    visible.Width, visible.Height),
                new Rectangle(source.X + visible.X - mapArea.X, source.Y + visible.Y - mapArea.Y,
                    visible.Width, visible.Height),
                Color.White);
        }

        void Fill(Rectangle mapArea, Color color)
        {
            var visible = Rectangle.Intersect(mapArea, crop);
            if (!visible.IsEmpty)
                batch.Draw(pixel, new Rectangle(destination.X + visible.X - crop.X,
                    destination.Y + visible.Y - crop.Y, visible.Width, visible.Height), color);
        }

        Rectangle MapArea(Rectangle screen) =>
            new(screen.X - CityMapLayout.Left, screen.Y - CityMapLayout.Top, screen.Width, screen.Height);

        var map = CityMapLayout.Bounds with { X = 0, Y = 0 };
        var neutralLayer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(null)];
        if (neutralLayer is not null)
            Draw(neutralLayer, map, map);
        for (var sectorId = 0; sectorId < state.Sectors.Count; sectorId++)
        {
            var sector = state.Sectors[sectorId];
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (sector.Owner is not null && layer is not null)
                Draw(layer, CityMapLayout.OwnershipSource(sectorId), CityMapLayout.OwnershipSource(sectorId));
            else if (neutralLayer is null)
                Fill(CityMapLayout.Source(sectorId), sector.Owner is { } owner
                    ? PlayerColors[owner.Value] * .68f
                    : new Color(24, 37, 39));
            if (_uiKeyedSprites is not null
                && ObjectiveSectorMarkerPresentation.IsMarked(state.Setup.Scenario, sectorId, sector.IsImportant))
                Draw(_uiKeyedSprites, OriginalSpriteLayout.ObjectiveSectorPylons, CityMapLayout.Source(sectorId));
        }
        if (_siteMarkerSprites is not null)
            foreach (var marker in CitySiteMarkerProjection.Project(
                         state, viewer, _siteSearchSelections.For(viewer)))
                Draw(_siteMarkerSprites, CitySiteMarkerProjection.Source(marker),
                    MapArea(CitySiteMarkerProjection.Destination(marker)));
        if (_uiKeyedSprites is null) return;
        // RULE-UI-006: the markers the map keeps after drawing every sector in number order.
        var markerFrames = GangStatusMarkerPresentation.MapFrames(state, viewer, _gangSight.For(state, viewer));
        for (var sectorId = 0; sectorId < markerFrames.Length; sectorId++)
            if (markerFrames[sectorId] >= 0)
                Draw(_uiKeyedSprites, OriginalSpriteLayout.GangStatus(markerFrames[sectorId]),
                    MapArea(GangStatusMarkerLayout.Destination(sectorId)));
    }

    /// <summary>
    /// A keyed label tab with its glyph (FND-UI-017, FND-UI-038). The original writes the glyph
    /// into the tab on the sheet and then copies the tab; the rebuild draws the tab and then the
    /// glyph in the sheet font's green.
    /// </summary>
    private void DrawGridLabel(SpriteBatch batch, PixelFont font, GridLabel label)
    {
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites,
                new Rectangle(label.Destination.X, label.Destination.Y, label.Source.Width, label.Source.Height),
                label.Source, Color.White);
        font.Draw(batch, label.Text,
            new Vector2(label.Destination.X + label.GlyphOffset.X, label.Destination.Y + label.GlyphOffset.Y),
            Color.Lime, 1);
    }

    /// <summary>
    /// The Overlord bar (FND-UI-017). A seat out of play shows the empty-seat art; a seat in play
    /// shows its portrait, from the row at source y 594 on the sector view when the active player
    /// sees none of its gangs in the sector. The viewed player's marker turns beside its portrait,
    /// and each seat in play has a planning light.
    /// </summary>
    private void DrawOverlordBar(SpriteBatch batch, Texture2D pixel, MatchState state, PlayerId? viewed, int? sectorView)
    {
        if (_uiSprites is null) return;
        var sightViewer = ViewingPlayer(state);
        for (var seat = 0; seat < MatchLimits.PlayerCount; seat++)
        {
            var player = state.Players.FirstOrDefault(candidate => candidate.Id.Value == seat);
            if (player is null || player.Status == PlayerStatus.Eliminated)
            {
                batch.Draw(_uiSprites, OverlordBarLayout.EmptySeat(seat),
                    OverlordBarLayout.EmptySeatSource(ActivePlayerMarkerPresentation.EmptySeatFrame(_inputTime)),
                    Color.White);
                continue;
            }
            var unseen = sectorView is { } sectorId
                && SectorOpponentGangs.InSector(state, sightViewer, player.Id, sectorId).Count == 0;
            batch.Draw(_uiSprites, OverlordBarLayout.Portrait(seat),
                unseen
                    ? OverlordBarLayout.UnseenPortraitSource(player.Setup.PortraitId)
                    : OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId),
                Color.White);
            batch.Draw(pixel, OverlordBarLayout.MarkerBackground(seat), Color.Black);
            if (viewed == player.Id)
                batch.Draw(_uiSprites, OverlordBarLayout.Marker(seat),
                    OriginalSpriteLayout.ActivePlayerMarker(ActivePlayerMarkerPresentation.Frame(_inputTime)),
                    Color.White);
            if (PlanningLightLit(player))
                batch.Draw(_uiSprites, OverlordBarLayout.PlanningLight(seat),
                    OverlordBarLayout.PlanningLightSource, Color.White);
            else
                batch.Draw(pixel, OverlordBarLayout.PlanningLight(seat), Color.Black);
        }
    }

    /// <summary>
    /// FND-UI-017: a human seat's light stays lit until its orders are in. Only the network code
    /// marks orders as in, so on a machine that seats every player the human seats stay lit; online
    /// a seat is lit while the turn waits on it.
    /// </summary>
    private bool PlanningLightLit(MatchPlayerState player)
    {
        if (_session is null)
            return OverlordBarLayout.PlanningLightLit(
                player.Setup.Controller == PlayerController.Human, ordersIn: false);
        var ownTurnSent = _online.PlanningIsSubmitted;
        var awaited = _online.AwaitedSlots.Contains(player.Id.Value);
        return OverlordBarLayout.PlanningLightLit(awaited,
            ordersIn: !SeatPlanningPresentation.IsDrafting(
                player.Id.Value, _session.Slot, _online.PlanningIsOpen || ownTurnSent, ownTurnSent,
                _online.AwaitedSlots, _online.ReadySlots));
    }
}

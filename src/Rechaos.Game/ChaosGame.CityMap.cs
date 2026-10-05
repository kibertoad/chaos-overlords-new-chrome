using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private readonly ActivePlayerMarkerClock _overlordMarkerClock = new();

    /// <summary>
    /// Draws the part <paramref name="crop"/> of the prepared city map, in map coordinates, with
    /// its corner at <paramref name="destination"/>. The map holds what FND-UI-025 has the city
    /// compositor draw into it: the neutral map, the owned interiors, the objective pylons, the
    /// site markers and the gang-status markers of <paramref name="viewer"/>. SCR-UI-003 shows all
    /// of it at <c>(2,42)</c>; SCR-UI-004's nine-sector display (FND-UI-018) and SCR-MOVE-001's
    /// neighbourhood (FND-MOVE-004) show a 162-by-156 crop, and SCR-UI-005 one sector's cell.
    /// </summary>
    private void DrawPreparedCityMap(
        SpriteBatch batch, Texture2D pixel, MatchState state, PlayerId viewer, Rectangle crop, Point destination)
    {
        var map = new CroppedMap(batch, pixel, crop, destination);
        var neutralLayer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(null)];
        var whole = CityMapLayout.Bounds with { X = 0, Y = 0 };
        if (neutralLayer is not null)
            map.Draw(neutralLayer, whole, whole);
        for (var sectorId = 0; sectorId < state.Sectors.Count; sectorId++)
        {
            var sector = state.Sectors[sectorId];
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (sector.Owner is not null && layer is not null)
                map.Draw(layer, CityMapLayout.OwnershipSource(sectorId), CityMapLayout.OwnershipSource(sectorId));
            else if (neutralLayer is null)
                map.Fill(CityMapLayout.Source(sectorId), sector.Owner is { } owner
                    ? PlayerColors[owner.Value] * .68f
                    : new Color(24, 37, 39));
            if (_uiKeyedSprites is not null
                && ObjectiveSectorMarkerPresentation.IsMarked(state.Setup.Scenario, sectorId, sector.IsImportant))
                map.Draw(_uiKeyedSprites, OriginalSpriteLayout.ObjectiveSectorPylons, CityMapLayout.Source(sectorId));
        }
        foreach (var marker in CitySiteMarkerProjection.Project(
                     state, viewer, _siteSearchSelections.For(viewer)))
        {
            var area = CityMapLayout.MapArea(CitySiteMarkerProjection.Destination(marker));
            if (_siteMarkerSprites is not null)
                map.Draw(_siteMarkerSprites, CitySiteMarkerProjection.Source(marker), area);
            else
                map.Outline(area, marker.Controlled ? Color.Lime : Color.Cyan);
        }
        // The markers have no drawn stand-in: without the sheet the map carries none, as before.
        if (_uiKeyedSprites is null) return;
        // RULE-UI-006: the markers the map keeps through the planning phase, and outside it those of
        // a draw of every sector in number order.
        var markerFrames = state.Coordinator.Phase == TurnPhase.Command
            ? _gangMarkers.Frames(state, viewer, _gangSight.For(state, viewer))
            : GangStatusMarkerPresentation.MapFrames(state, viewer, _gangSight.For(state, viewer));
        for (var sectorId = 0; sectorId < markerFrames.Length; sectorId++)
            if (markerFrames[sectorId] >= 0)
                map.Draw(_uiKeyedSprites, OriginalSpriteLayout.GangStatus(markerFrames[sectorId]),
                    CityMapLayout.MapArea(GangStatusMarkerLayout.Destination(sectorId)));
    }

    /// <summary>
    /// Blits parts of the prepared city map at 1:1: each part is given by where it lies on the map,
    /// and only what falls inside the crop is drawn, moved so the crop's corner is at the destination.
    /// </summary>
    private readonly record struct CroppedMap(SpriteBatch Batch, Texture2D Pixel, Rectangle Crop, Point Destination)
    {
        /// <summary>Draws <paramref name="source"/> of the texture, which covers <paramref name="mapArea"/>.</summary>
        public void Draw(Texture2D texture, Rectangle source, Rectangle mapArea)
        {
            var visible = Rectangle.Intersect(mapArea, Crop);
            if (visible.IsEmpty) return;
            Batch.Draw(texture, Target(visible),
                new Rectangle(source.X + visible.X - mapArea.X, source.Y + visible.Y - mapArea.Y,
                    visible.Width, visible.Height),
                Color.White);
        }

        public void Fill(Rectangle mapArea, Color color)
        {
            var visible = Rectangle.Intersect(mapArea, Crop);
            if (!visible.IsEmpty) Batch.Draw(Pixel, Target(visible), color);
        }

        /// <summary>A 1-pixel frame just inside <paramref name="mapArea"/>.</summary>
        public void Outline(Rectangle mapArea, Color color)
        {
            Fill(mapArea with { Height = 1 }, color);
            Fill(mapArea with { Y = mapArea.Bottom - 1, Height = 1 }, color);
            Fill(mapArea with { Width = 1 }, color);
            Fill(mapArea with { X = mapArea.Right - 1, Width = 1 }, color);
        }

        private Rectangle Target(Rectangle visible) =>
            new(Destination.X + visible.X - Crop.X, Destination.Y + visible.Y - Crop.Y,
                visible.Width, visible.Height);
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
    /// sees none of its gangs in the sector (<paramref name="seatsSeen"/>, null off the sector
    /// view). The viewed player's marker turns beside its portrait, and each seat in play has a
    /// planning light.
    /// </summary>
    private void DrawOverlordBar(
        SpriteBatch batch, Texture2D pixel, MatchState state, PlayerId? viewed, IReadOnlyList<bool>? seatsSeen)
    {
        if (_uiSprites is null) return;
        var markerFrame = _referenceFrame?.MarkerFrame ?? _overlordMarkerClock.Frame(_inputTime);
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
            batch.Draw(_uiSprites, OverlordBarLayout.Portrait(seat),
                seatsSeen is not null && !seatsSeen[seat]
                    ? OverlordBarLayout.UnseenPortraitSource(player.Setup.PortraitId)
                    : OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId),
                Color.White);
            batch.Draw(pixel, OverlordBarLayout.MarkerBackground(seat), Color.Black);
            if (viewed == player.Id)
                batch.Draw(_uiSprites, OverlordBarLayout.Marker(seat),
                    OriginalSpriteLayout.ActivePlayerMarker(markerFrame), Color.White);
            if (PlanningLightLit(state, player))
                batch.Draw(_uiSprites, OverlordBarLayout.PlanningLight(seat),
                    OverlordBarLayout.PlanningLightSource, Color.White);
            else
                batch.Draw(pixel, OverlordBarLayout.PlanningLight(seat), Color.Black);
        }
    }

    /// <summary>
    /// FND-UI-017, FND-UI-043: human seats wait until their planning visit completes,
    /// including local seats. Final visits retain the completed round's dark lights. Online the
    /// turn waits only on human seats, so a seat is lit while the turn is still waiting on its
    /// orders.
    /// </summary>
    private bool PlanningLightLit(MatchState state, MatchPlayerState player)
    {
        if (_session is null)
            return OverlordBarLayout.PlanningLightLit(
                player.Setup.Controller == PlayerController.Human,
                OverlordBarLayout.LocalOrdersIn(state, player.Id));
        var ownTurnSent = _online.PlanningIsSubmitted;
        return SeatPlanningPresentation.IsDrafting(
            player.Id.Value, _session.Slot, _online.PlanningIsOpen || ownTurnSent, ownTurnSent,
            _online.AwaitedSlots, _online.ReadySlots);
    }
}

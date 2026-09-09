using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void FinishHandoff()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId)
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        if (LastTurnReports(_state, playerId).Count == 0)
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        _eventCursor = 0;
        _managementReturnScreen = ClientScreen.City;
        _screens.Show(ClientScreen.Events);
    }

    private void HandleEventsClick(Point point)
    {
        if (LastTurnEventsLayout.Previous.Contains(point)) MoveEventCursor(-1);
        else if (LastTurnEventsLayout.Next.Contains(point)) MoveEventCursor(1);
        else if (LastTurnEventsLayout.Ok.Contains(point)) CloseEvents();
    }

    private void MoveEventCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = LastTurnReports(_state, playerId).Count;
        if (count > 0) _eventCursor = Mod(_eventCursor + delta, count);
    }

    private void CloseEvents()
    {
        if (_state?.Coordinator.ActivePlayer is { } playerId && _replay is not null)
        {
            var count = _state.NotificationsFor(playerId).Count;
            for (var index = 0; index < count; index++)
                _replay.TryDismissNotification(playerId, out _);
        }
        _eventCursor = 0;
        _screens.Show(_managementReturnScreen);
    }

    private void DrawLastTurnEventsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        if (_lastTurnEventsBackground is not null)
            batch.Draw(_lastTurnEventsBackground, LastTurnEventsLayout.Panel, Color.White);
        else
            batch.Draw(pixel, LastTurnEventsLayout.Panel, new Color(0, 0, 0, 245));
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var notifications = LastTurnReports(state, playerId);
        ClearLastTurnEventFields(batch, pixel);
        if (notifications.Count == 0)
        {
            font.Draw(batch, "NO EVENTS TO REPORT", new Vector2(253, 221), Color.Lime, 1);
            return;
        }

        _eventCursor = Math.Clamp(_eventCursor, 0, notifications.Count - 1);
        var notification = notifications[_eventCursor];
        font.Draw(batch, $"{_eventCursor + 1:00} OF {notifications.Count:00}",
            new Vector2(136, 137), Color.Lime, 1);
        DrawEventArtwork(batch, state, notification);
        var heading = $"DATE {MatchDate(notification.Turn)} OBJECT {EventObject(state, notification)}";
        if (heading.Length > 36) heading = heading[..36];
        font.Draw(batch, heading, new Vector2(221, 313), Color.White, 1);
        var status = NotificationPresentation.LastTurnStatus(notification);
        if (status.Length > 35) status = status[..35];
        font.Draw(batch, "STATUS " + status, new Vector2(221, 322), Color.Lime, 1);
    }

    private void DrawEventArtwork(SpriteBatch batch, MatchState state, GameNotification notification)
    {
        batch.Draw(_pixel!, LastTurnEventsLayout.Artwork, Color.Black);
        if (notification.SectorId is { } sectorId)
        {
            var sector = state.Sectors[sectorId];
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (layer is not null)
                batch.Draw(layer, LastTurnEventsLayout.Artwork, CityMapLayout.Source(sectorId), Color.White);
        }
        if (notification.Gang is { } gangId && state.FindGang(gangId) is { } gang && _gangPortraits is not null)
        {
            var portrait = new Rectangle(
                LastTurnEventsLayout.Artwork.Right - 76,
                LastTurnEventsLayout.Artwork.Y + 12, 64, 64);
            batch.Draw(_gangPortraits, portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        }
    }

    private static string EventObject(MatchState state, GameNotification notification)
    {
        if (notification.Gang is { } gangId && state.FindGang(gangId) is { } gang)
            return state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).Name;
        if (notification.SectorId is { } sectorId) return SectorCode(sectorId);
        return notification.Kind.ToString().ToUpperInvariant();
    }

    private static IReadOnlyList<GameNotification> LastTurnReports(MatchState state, PlayerId playerId)
    {
        var reports = new List<GameNotification>();
        var sectorMilestones = new HashSet<(int Turn, GameNotificationKind Kind, int? Sector)>();
        foreach (var notification in state.NotificationsFor(playerId))
        {
            var related = notification.RelatedEventSequence is { } sequence
                ? state.Events.FirstOrDefault(gameEvent => gameEvent.Sequence == sequence)
                : null;
            if (!NotificationPresentation.IsLastTurnReport(notification, related)) continue;
            if ((notification.Kind is GameNotificationKind.Control or GameNotificationKind.Influence)
                && !sectorMilestones.Add((notification.Turn, notification.Kind, notification.SectorId)))
                continue;
            reports.Add(notification);
        }
        return reports;
    }

    private static void ClearLastTurnEventFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, LastTurnEventsLayout.Page, Color.Black);
        batch.Draw(pixel, new Rectangle(221, 311, 221, 19), Color.Black);
    }
}

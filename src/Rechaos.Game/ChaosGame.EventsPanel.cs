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
            StartPlanningTimer(_inputTime);
            return;
        }
        StartPlanningTimer(_inputTime);
        if (AudioRouting.IncomingMessageSound(_state.ComlinkFor(playerId).HasUnread) is { } alert)
            PlayGeneralSound(alert);
        var reports = LastTurnReports(_state, playerId);
        var hasCombat = VisibleCombatResults(_state, playerId).Count > 0;
        if (reports.Count == 0)
        {
            if (hasCombat) OpenCombatResults(ClientScreen.City);
            else _screens.Show(ClientScreen.City);
            return;
        }
        _openCombatAfterEvents = hasCombat;
        _managementReturnScreen = ClientScreen.City;
        BeginEventReview(reports.Count);
        _screens.Show(ClientScreen.Events);
    }

    private void OpenEvents(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = LastTurnReports(_state, playerId).Count;
        if (count == 0)
        {
            _message = "NO EVENTS TO REPORT";
            return;
        }
        _managementReturnScreen = returnScreen;
        BeginEventReview(count);
        _screens.Show(ClientScreen.Events);
    }

    private void BeginEventReview(int count)
    {
        _eventCursor = 0;
        _eventViewedPages.Clear();
        if (count > 0) _eventViewedPages.Add(0);
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
        if (count > 0)
        {
            _eventCursor = Mod(_eventCursor + delta, count);
            _eventViewedPages.Add(_eventCursor);
        }
    }

    private void CloseEvents()
    {
        if (_state?.Coordinator.ActivePlayer is { } playerId && _actions is not null)
        {
            var reportCount = LastTurnReports(_state, playerId).Count;
            if (EventReviewProgress.IsComplete(reportCount, _eventViewedPages))
            {
                var count = _state.NotificationsFor(playerId).Count;
                for (var index = 0; index < count; index++)
                    _actions.DismissNotification(playerId);
            }
        }
        _eventCursor = 0;
        _eventViewedPages.Clear();
        if (_openCombatAfterEvents)
        {
            _openCombatAfterEvents = false;
            OpenCombatResults(_managementReturnScreen);
        }
        else
        {
            _screens.Show(_managementReturnScreen);
        }
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
        DrawEventArtwork(batch, pixel, state, notification);
        var heading = $"DATE {MatchDate(notification.Turn)} OBJECT {EventObject(state, notification)}";
        if (heading.Length > 40) heading = heading[..40];
        font.Draw(batch, heading, new Vector2(198, 293), Color.White, 1);
        var status = NotificationPresentation.LastTurnStatus(notification);
        if (status.Length > 39) status = status[..39];
        font.Draw(batch, "STATUS " + status, new Vector2(198, 302), Color.Lime, 1);
    }

    private void DrawEventArtwork(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        GameNotification notification)
    {
        batch.Draw(pixel, LastTurnEventsLayout.Artwork, Color.Black);
        var related = RelatedEvent(state, notification);
        var artworkIndex = notification.Kind switch
        {
            GameNotificationKind.Crackdown => 1,
            GameNotificationKind.Control => 2,
            GameNotificationKind.ControlLost => 3,
            GameNotificationKind.Elimination when related?.Kind == GameEventKind.PlayerEliminated => 9,
            GameNotificationKind.Elimination => 4,
            GameNotificationKind.Research => 5,
            GameNotificationKind.Influence => 6,
            GameNotificationKind.Objective => 7,
            _ => 0
        };
        if (artworkIndex > 0 && _lastTurnEventArtwork[artworkIndex] is { } artwork)
            batch.Draw(artwork, LastTurnEventsLayout.Artwork, Color.White);
    }

    private static string EventObject(MatchState state, GameNotification notification)
    {
        var related = RelatedEvent(state, notification);
        if (related?.Elimination is { } elimination)
            return state.FindPlayer(elimination.EliminatedPlayer)?.Setup.Name.ToUpperInvariant()
                ?? $"PLAYER {elimination.EliminatedPlayer.Value + 1}";
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
            var related = RelatedEvent(state, notification);
            if (!NotificationPresentation.IsLastTurnReport(notification, related)) continue;
            if ((notification.Kind is GameNotificationKind.Control or GameNotificationKind.Influence)
                && !sectorMilestones.Add((notification.Turn, notification.Kind, notification.SectorId)))
                continue;
            reports.Add(notification);
        }
        return reports;
    }

    private static GameEvent? RelatedEvent(MatchState state, GameNotification notification) =>
        notification.RelatedEventSequence is { } sequence
            ? state.Events.FirstOrDefault(gameEvent => gameEvent.Sequence == sequence)
            : null;

    private static void ClearLastTurnEventFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, LastTurnEventsLayout.Page, Color.Black);
        batch.Draw(pixel, new Rectangle(198, 291, 242, 19), Color.Black);
    }
}

public static class EventReviewProgress
{
    public static bool IsComplete(int pageCount, IReadOnlySet<int> viewedPages)
    {
        ArgumentNullException.ThrowIfNull(viewedPages);
        if (pageCount < 0) throw new ArgumentOutOfRangeException(nameof(pageCount));
        return pageCount == 0 || Enumerable.Range(0, pageCount).All(viewedPages.Contains);
    }
}

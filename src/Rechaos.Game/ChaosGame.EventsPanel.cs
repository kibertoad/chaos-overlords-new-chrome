using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void HandleHandoffClick(Point point)
    {
        if (!HandoffReady.Contains(point)) return;
        PlayGeneralSound(AudioRouting.PointerPushSound());
        FinishHandoff();
    }

    private void FinishHandoff()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId)
        {
            _screens.Show(ClientScreen.City);
            StartPlanningTimer(_inputTime);
            return;
        }
        PrepareCurrentHireOffers();
        StartPlanningTimer(_inputTime);
        UpdateComlinkAlert(_inputTime, enteringPlanning: true);
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

    private void UpdateComlinkAlert(TimeSpan now, bool enteringPlanning = false)
    {
        var hasUnread = _state?.Coordinator.ActivePlayer is { } playerId
            && _state.Outcome is null
            && _state.ComlinkFor(playerId).HasUnread;
        var presentationActive = enteringPlanning || _screens.Current is not (
            ClientScreen.Title or ClientScreen.Setup or ClientScreen.Online
            or ClientScreen.Lobby or ClientScreen.Handoff or ClientScreen.Endgame);
        if (_comlinkAlertCadence.Advance(hasUnread, presentationActive, now)
            && AudioRouting.IncomingMessageSound(hasUnread) is { } alert)
            PlayGeneralSound(alert);
    }

    private void OpenEvents(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = ReviewableReports(_state, playerId).Count;
        if (count == 0)
        {
            RejectInput("NO EVENTS TO REPORT");
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
        else if (LastTurnEventsLayout.Ok.Contains(point))
        {
            AcceptInput();
            CloseEvents();
        }
    }

    private void MoveEventCursor(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var count = ReviewableReports(_state, playerId).Count;
        if (count > 0)
        {
            var next = BoundedPageNavigation.Move(_eventCursor, count, delta);
            PlayGeneralSound(AudioRouting.PageNavigationSound(next != _eventCursor));
            if (next != _eventCursor)
            {
                _eventCursor = next;
                _eventViewedPages.Add(_eventCursor);
            }
        }
    }

    private void CloseEvents()
    {
        if (_state?.Coordinator.ActivePlayer is { } playerId && _actions is not null)
        {
            var currentReports = LastTurnReports(_state, playerId);
            var reportCount = ReviewableReports(_state, playerId).Count;
            if (currentReports.Count > 0
                && EventReviewProgress.IsComplete(reportCount, _eventViewedPages))
            {
                _lastTurnEventArchive.Store(playerId, currentReports);
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

    private void DrawLastTurnEventsFrame(
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
        var notifications = ReviewableReports(state, playerId);
        ClearLastTurnEventFields(batch, pixel);
        batch.Draw(pixel, LastTurnEventsLayout.Artwork, Color.Black);
        if (notifications.Count == 0)
            return;

        _eventCursor = Math.Clamp(_eventCursor, 0, notifications.Count - 1);
    }

    private GameNotification? CurrentEventReport(MatchState state)
    {
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var notifications = ReviewableReports(state, playerId);
        if (notifications.Count == 0) return null;
        _eventCursor = Math.Clamp(_eventCursor, 0, notifications.Count - 1);
        return notifications[_eventCursor];
    }

    private void DrawLastTurnEventContent(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        GameNotification? notification)
    {
        if (notification is null)
        {
            font.Draw(batch, "NO EVENTS TO REPORT", new Vector2(253, 221), Color.Lime, 1);
            return;
        }

        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var reportCount = ReviewableReports(state, playerId).Count;
        font.Draw(batch, $"{_eventCursor + 1:00} OF {reportCount:00}",
            new Vector2(136, 137), Color.Lime, 1);
        DrawEventArtworkForeground(batch, state, notification);
        font.Draw(batch, MatchDate(notification.Turn),
            new Vector2(LastTurnEventsLayout.DateValue.X, LastTurnEventsLayout.DateValue.Y),
            Color.Lime, 1);
        var eventObject = EventObject(state, notification);
        var objectColumns = LastTurnEventsLayout.ObjectValue.Width / OriginalFontLayout.CellWidth;
        if (eventObject.Length > objectColumns) eventObject = eventObject[..objectColumns];
        font.Draw(batch, eventObject,
            new Vector2(LastTurnEventsLayout.ObjectValue.X, LastTurnEventsLayout.ObjectValue.Y),
            Color.Lime, 1);
        var status = NotificationPresentation.LastTurnStatus(notification);
        var statusColumns = LastTurnEventsLayout.StatusValue.Width / OriginalFontLayout.CellWidth;
        if (status.Length > statusColumns) status = status[..statusColumns];
        font.Draw(batch, status,
            new Vector2(LastTurnEventsLayout.StatusValue.X, LastTurnEventsLayout.StatusValue.Y),
            Color.Lime, 1);
    }

    private bool DrawInfluenceSiteBackground(
        SpriteBatch batch,
        MatchState state,
        GameNotification notification)
    {
        var related = RelatedEvent(state, notification);
        if (LastTurnEventPresentation.InfluenceSiteId(notification, related) is not { } siteId
            || state.FindSite(siteId) is not { } site
            || _sitePortraits is null)
            return false;
        batch.Draw(_sitePortraits, LastTurnEventsLayout.Artwork,
            LastTurnEventPresentation.SiteBackgroundSource(site.DefinitionId), Color.White);
        return true;
    }

    private void DrawEventArtworkForeground(
        SpriteBatch batch,
        MatchState state,
        GameNotification notification)
    {
        var related = RelatedEvent(state, notification);
        var artworkIndex = LastTurnEventPresentation.ArtworkIndex(notification, related);
        if (artworkIndex > 0 && _lastTurnEventArtwork[artworkIndex] is { } artwork)
            batch.Draw(artwork, LastTurnEventsLayout.Artwork, Color.White);
        if (LastTurnEventPresentation.ResearchItemId(notification, related) is { } itemId
            && itemId >= 0 && itemId < _itemRotationTextures.Length
            && _itemRotationTextures[itemId] is { } rotation)
            batch.Draw(rotation, LastTurnEventsLayout.ResearchItem,
                ItemRotationPresentation.Frame(_inputTime), Color.White);
    }

    private static string EventObject(MatchState state, GameNotification notification)
    {
        var related = RelatedEvent(state, notification);
        if (related?.Elimination is { } elimination)
            return state.FindPlayer(elimination.EliminatedPlayer)?.Setup.Name.ToUpperInvariant()
                ?? $"PLAYER {elimination.EliminatedPlayer.Value + 1}";
        if (LastTurnEventPresentation.ResearchItemId(notification, related) is { } itemId)
            return state.Definitions.Items[itemId].Name;
        if (LastTurnEventPresentation.InfluenceSiteObject(state, notification, related) is { } site)
            return site;
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

    private IReadOnlyList<GameNotification> ReviewableReports(MatchState state, PlayerId playerId)
    {
        var current = LastTurnReports(state, playerId);
        return current.Count > 0 ? current : _lastTurnEventArchive.For(playerId);
    }

    private static GameEvent? RelatedEvent(MatchState state, GameNotification notification) =>
        notification.RelatedEventSequence is { } sequence
            ? state.Events.FirstOrDefault(gameEvent => gameEvent.Sequence == sequence)
            : null;

    private static void ClearLastTurnEventFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, LastTurnEventsLayout.Page, Color.Black);
        batch.Draw(pixel, LastTurnEventsLayout.DateValue, Color.Black);
        batch.Draw(pixel, LastTurnEventsLayout.ObjectValue, Color.Black);
        batch.Draw(pixel, LastTurnEventsLayout.StatusValue, Color.Black);
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

public sealed class LastTurnEventArchive
{
    private readonly Dictionary<PlayerId, IReadOnlyList<GameNotification>> _reports = [];

    public void Store(PlayerId player, IEnumerable<GameNotification> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        _reports[player] = reports.ToArray();
    }

    public IReadOnlyList<GameNotification> For(PlayerId player) =>
        _reports.TryGetValue(player, out var reports) ? reports : [];

    public void Clear() => _reports.Clear();
}

public static class LastTurnEventPresentation
{
    public const int NativeDitherPatternSize = 8;

    public static int ArtworkIndex(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.Kind switch
        {
            GameNotificationKind.Crackdown => 1,
            GameNotificationKind.Control => 2,
            GameNotificationKind.ControlLost => 3,
            GameNotificationKind.Elimination when relatedEvent?.Kind == GameEventKind.PlayerEliminated => 9,
            GameNotificationKind.Elimination => 4,
            GameNotificationKind.Research => 5,
            GameNotificationKind.Influence => 4,
            GameNotificationKind.Objective => 7,
            _ => 0
        };
    }

    public static int? ResearchItemId(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.Kind == GameNotificationKind.Research
            && relatedEvent?.Action == GangAction.Research
            && relatedEvent.Target.Kind == CommandTargetKind.Item
                ? relatedEvent.Target.Id
                : null;
    }

    public static int? InfluenceSiteId(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.Kind == GameNotificationKind.Influence
            && relatedEvent?.Action == GangAction.Influence
            && relatedEvent.Target.Kind == CommandTargetKind.Site
                ? relatedEvent.Target.Id
                : null;
    }

    public static string? InfluenceSiteObject(
        MatchState state,
        GameNotification notification,
        GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (InfluenceSiteId(notification, relatedEvent) is not { } siteId
            || state.FindSite(siteId) is not { } site)
            return null;
        var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
        return $"{siteId:00}:{definition.Name}";
    }

    public static Rectangle SiteBackgroundSource(short definitionId)
    {
        var portrait = OriginalSpriteLayout.SitePortrait(definitionId);
        return new Rectangle(portrait.X + 12, portrait.Y + 1, 94, 62);
    }

    public static bool NativeDitherKeepsPixel(int x, int y)
    {
        if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
        return (x & 3) == ((y & 1) << 1);
    }

    public static Texture2D CreateEventSiteDitherOverlay(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var width = LastTurnEventsLayout.Artwork.Width;
        var height = LastTurnEventsLayout.Artwork.Height;
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            pixels[y * width + x] = NativeDitherKeepsPixel(x, y)
                ? Color.Transparent
                : Color.Black;
        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(pixels);
        return texture;
    }
}

public static class ItemRotationPresentation
{
    public const int FrameCount = 15;
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(80);

    public static Rectangle Frame(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var frame = (int)(elapsed.TotalMilliseconds / FrameDuration.TotalMilliseconds)
            % FrameCount;
        return new Rectangle(frame * 48, 0, 48, 48);
    }
}

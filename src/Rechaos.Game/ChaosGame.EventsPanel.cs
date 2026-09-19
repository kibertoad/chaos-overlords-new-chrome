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
        // An eliminated local player first receives the same private next-player card. Its Ready
        // control then leads to the original terminal notice instead of opening the succeeding
        // player's planning turn beneath the departed player's name.
        if (_eliminationHandoffPlayer is not null)
        {
            _screens.Show(ClientScreen.Elimination);
            return;
        }
        if (_state?.Coordinator.ActivePlayer is not { } playerId)
        {
            _screens.Show(ClientScreen.City);
            StartPlanningTimer(_inputTime);
            return;
        }
        PrepareCurrentHireOffers();
        _deferComlinkAlertUntilPlanningVisible = true;
        if (_showGameInfoAtPlanningEntry)
        {
            _showGameInfoAtPlanningEntry = false;
            _continuePlanningEntryAfterGameInfo = true;
            _screens.Show(ClientScreen.GameInfo);
            return;
        }
        ShowTurnReportsOrCity();
    }

    private void ShowTurnReportsOrCity()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId)
        {
            _screens.Show(ClientScreen.City);
            return;
        }
        var reports = LastTurnReports(_state, playerId);
        var hasCombat = VisibleCombatResults(_state, playerId).Count > 0;
        _openEventsAfterCombat = false;
        _automaticDetailedCombatPresentation = false;
        switch (HandoffPresentationOrder.First(hasCombat, reports.Count > 0))
        {
            case HandoffPresentationStep.Combat:
                // The original's planning entry displays the automatic combat presentation before
                // it opens Last Turn Events. Detailed Combat remains a bounded recreation overlay,
                // but starts over the city rather than after the private event review.
                _openEventsAfterCombat = reports.Count > 0;
                _managementReturnScreen = ClientScreen.City;
                if (_detailedCombat && _combatAnimationTextures.Count > 0)
                {
                    _automaticDetailedCombatPresentation = true;
                    _screens.Show(ClientScreen.City);
                }
                else
                {
                    OpenCombatResults(ClientScreen.City);
                }
                return;
            case HandoffPresentationStep.Events:
                _managementReturnScreen = ClientScreen.City;
                BeginEventReview(reports.Count);
                _screens.Show(ClientScreen.Events);
                return;
            default:
                _screens.Show(ClientScreen.City);
                CompletePlanningEntryPresentation();
                return;
        }
    }

    private void FinishAutomaticCombatPresentation()
    {
        _automaticDetailedCombatPresentation = false;
        if (!_openEventsAfterCombat)
        {
            _screens.Show(_managementReturnScreen);
            CompletePlanningEntryPresentation();
            return;
        }

        _openEventsAfterCombat = false;
        if (_state?.Coordinator.ActivePlayer is not { } playerId)
        {
            _screens.Show(_managementReturnScreen);
            return;
        }
        var reports = LastTurnReports(_state, playerId);
        if (reports.Count == 0)
        {
            _screens.Show(_managementReturnScreen);
            CompletePlanningEntryPresentation();
            return;
        }
        BeginEventReview(reports.Count);
        _screens.Show(ClientScreen.Events);
    }

    private void UpdateComlinkAlert(TimeSpan now, bool enteringPlanning = false)
    {
        var hasUnread = _state?.Coordinator.ActivePlayer is { } playerId
            && _state.Outcome is null
            && _state.ComlinkFor(playerId).HasUnread;
        var presentationActive = !_deferComlinkAlertUntilPlanningVisible
            && (enteringPlanning || _screens.Current is not (
            ClientScreen.Title or ClientScreen.Setup or ClientScreen.Online
            or ClientScreen.Lobby or ClientScreen.Handoff or ClientScreen.Elimination
            or ClientScreen.Endgame));
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
                _lastTurnEventArchive.Store(playerId, _state.Coordinator.Turn, currentReports);
                var count = _state.NotificationsFor(playerId).Count;
                for (var index = 0; index < count; index++)
                    _actions.DismissNotification(playerId);
            }
        }
        _eventCursor = 0;
        _eventViewedPages.Clear();
        _screens.Show(_managementReturnScreen);
        CompletePlanningEntryPresentation();
    }

    private void CloseGameInformation()
    {
        if (_continuePlanningEntryAfterGameInfo)
        {
            _continuePlanningEntryAfterGameInfo = false;
            ShowTurnReportsOrCity();
            return;
        }

        _screens.Show(_managementReturnScreen);
    }

    private void CompletePlanningEntryPresentation()
    {
        if (!_deferComlinkAlertUntilPlanningVisible) return;
        _deferComlinkAlertUntilPlanningVisible = false;
        StartPlanningTimer(_inputTime);
        UpdateComlinkAlert(_inputTime, enteringPlanning: true);
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
            font.Draw(batch, "NO EVENTS TO REPORT",
                new Vector2(SharedPanelLayout.X(149), SharedPanelLayout.Y(96)), Color.Lime, 1);
            return;
        }

        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var reportCount = ReviewableReports(state, playerId).Count;
        font.Draw(batch, $"{_eventCursor + 1:00} OF {reportCount:00}",
            new Vector2(SharedPanelLayout.X(34), SharedPanelLayout.Y(13)), Color.Lime, 1);
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
        var status = NotificationPresentation.LastTurnStatus(notification, RelatedEvent(state, notification));
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
        if (related?.Hire is { } hire)
            return state.Definitions.Gangs.Single(value => value.Id == hire.GangDefinitionId).Name;
        if (notification.Gang is { } gangId && state.FindGang(gangId) is { } gang)
            return state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).Name;
        if (notification.SectorId is { } sectorId) return SectorCode(sectorId);
        return notification.Kind.ToString().ToUpperInvariant();
    }

    private static IReadOnlyList<GameNotification> LastTurnReports(MatchState state, PlayerId playerId)
        => LastTurnEventProjection.For(state, playerId);

    private IReadOnlyList<GameNotification> ReviewableReports(MatchState state, PlayerId playerId)
    {
        var current = LastTurnReports(state, playerId);
        return current.Count > 0
            ? current
            : _lastTurnEventArchive.For(playerId, state.Coordinator.Turn);
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

public enum HandoffPresentationStep
{
    City,
    Combat,
    Events
}

/// <summary>
/// The original planning entry is synchronous: combat is presented before private turn reports.
/// This keeps both automatic presentation paths on that shared, testable order.
/// </summary>
public static class HandoffPresentationOrder
{
    public static HandoffPresentationStep First(bool hasCombat, bool hasReports) => hasCombat
        ? HandoffPresentationStep.Combat
        : hasReports ? HandoffPresentationStep.Events : HandoffPresentationStep.City;
}

public static class LastTurnEventProjection
{
    public static IReadOnlyList<GameNotification> For(MatchState state, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Select(
            state.NotificationsFor(playerId),
            state.Events,
            state.Coordinator.Turn - 1);
    }

    public static IReadOnlyList<GameNotification> Select(
        IEnumerable<GameNotification> notifications,
        IReadOnlyList<GameEvent> events,
        int completedTurn)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(events);
        if (completedTurn < 1) return [];

        var eventsBySequence = events.ToDictionary(gameEvent => gameEvent.Sequence);
        var reports = new List<GameNotification>(MatchLimits.LastTurnReportsPerPlayer);
        var sectorMilestones = new HashSet<(GameNotificationKind Kind, int? Sector)>();
        foreach (var notification in notifications)
        {
            if (notification.Turn != completedTurn) continue;
            var related = notification.RelatedEventSequence is { } sequence
                && eventsBySequence.TryGetValue(sequence, out var gameEvent)
                    ? gameEvent
                    : null;
            if (!NotificationPresentation.IsLastTurnReport(notification, related)) continue;
            if ((notification.Kind is GameNotificationKind.Control or GameNotificationKind.Influence)
                && !sectorMilestones.Add((notification.Kind, notification.SectorId)))
                continue;
            reports.Add(notification);
            // The original recorder retains the first 32 records and ignores
            // every later write until the next whole-turn resolution reset.
            if (reports.Count == MatchLimits.LastTurnReportsPerPlayer) break;
        }
        return reports;
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
    private readonly Dictionary<PlayerId, ArchivedEventReports> _reports = [];

    public void Store(PlayerId player, int reviewTurn, IEnumerable<GameNotification> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        if (reviewTurn < 1) throw new ArgumentOutOfRangeException(nameof(reviewTurn));
        _reports[player] = new ArchivedEventReports(reviewTurn, reports.ToArray());
    }

    public IReadOnlyList<GameNotification> For(PlayerId player, int reviewTurn)
    {
        if (reviewTurn < 1) throw new ArgumentOutOfRangeException(nameof(reviewTurn));
        if (!_reports.TryGetValue(player, out var archive)) return [];
        if (archive.ReviewTurn == reviewTurn) return archive.Reports;
        _reports.Remove(player);
        return [];
    }

    public void Remove(PlayerId player) => _reports.Remove(player);

    public void Clear() => _reports.Clear();

    private sealed record ArchivedEventReports(
        int ReviewTurn,
        IReadOnlyList<GameNotification> Reports);
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
        => OriginalPatternMask.PreservesDestination(OriginalPatternMask.Sparse, x, y);

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
        return Frame(frame);
    }

    public static Rectangle Frame(int frame)
    {
        if (frame is < 0 or >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle(frame * 48, 0, 48, 48);
    }
}

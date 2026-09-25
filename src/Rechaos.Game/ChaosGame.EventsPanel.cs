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
        // RULE-SETUP-008: after the Ready card, Game Information for each local human who plans in
        // the round a loaded match resumed on, then combat results, Last Turn Events and the Comlink.
        if (_resumedMatchTurn == _state.Coordinator.Turn && _resumedGameInfoShown.Add(playerId))
        {
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
        _eventPageShownAt = _inputTime;
    }

    private void HandleEventsClick(Point point)
    {
        if (LastTurnEventsLayout.Previous.Contains(point)) MoveEventCursor(-1);
        else if (LastTurnEventsLayout.Next.Contains(point)) MoveEventCursor(1);
        else if (LastTurnEventsLayout.Ok.Contains(point))
            AcceptAndInvoke(CloseEvents);
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
                _eventPageShownAt = _inputTime;
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
        UpdateComlinkAlert(_inputTime, enteringPlanning: true);
        StartPlanningTimer(_inputTime);
    }

    private void DrawLastTurnEventsFrame(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawPanelArtwork(batch, pixel, _lastTurnEventsBackground, LastTurnEventsLayout.Panel);
        var playerId = ViewingPlayer(state);
        var notifications = ReviewableReports(state, playerId);
        ClearLastTurnEventFields(batch, pixel);
        batch.Draw(pixel, LastTurnEventsLayout.Artwork, Color.Black);
        if (notifications.Count == 0)
            return;

        _eventCursor = Math.Clamp(_eventCursor, 0, notifications.Count - 1);
    }

    private GameNotification? CurrentEventReport(MatchState state)
    {
        var playerId = ViewingPlayer(state);
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

        var playerId = ViewingPlayer(state);
        var reportCount = ReviewableReports(state, playerId).Count;
        // SCR-EVENT-001: page number and count as two digits each, with the panel art's OF
        // between them, and the arrow faces, greyed on the first and last page. A black cell
        // stands for the opaque copy of each digit cell.
        DrawDigitCells(batch, pixel, font, $"{Math.Min(_eventCursor + 1, 99):00}",
            LastTurnEventsLayout.PageNumber);
        DrawDigitCells(batch, pixel, font, $"{Math.Min(reportCount, 99):00}",
            LastTurnEventsLayout.PageCount);
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, LastTurnEventsLayout.Previous,
                LastTurnEventsLayout.PreviousSource(firstPage: _eventCursor == 0), Color.White);
            batch.Draw(_uiSprites, LastTurnEventsLayout.Next,
                LastTurnEventsLayout.NextSource(lastPage: _eventCursor == reportCount - 1),
                Color.White);
        }
        DrawEventArtworkForeground(batch, state, notification);
        // SCR-EVENT-001: the date is elapsed_turns (turns completed) as year and week, drawn
        // over the panel art's 0000.00 and left out at 0.
        if (LastTurnEventsLayout.Date(state.Coordinator.Turn - 1) is var (year, week))
        {
            DrawDigitCells(batch, pixel, font, year, LastTurnEventsLayout.Year);
            DrawDigitCells(batch, pixel, font, week, LastTurnEventsLayout.Week);
        }
        // SCR-EVENT-001: the subject is not cut; only a cash report's gang name is, to 20
        // characters, inside LastTurnEventPresentation.Subject.
        font.Draw(batch, EventObject(state, notification),
            LastTurnEventsLayout.Subject.ToVector2(), Color.Lime, 1);
        var status = NotificationPresentation.LastTurnStatus(notification, RelatedEvent(state, notification));
        if (status.Length > LastTurnEventsLayout.CaptionColumns)
            status = status[..LastTurnEventsLayout.CaptionColumns];
        font.Draw(batch, status, LastTurnEventsLayout.Caption.ToVector2(), Color.Lime, 1);
    }

    private static void DrawDigitCells(
        SpriteBatch batch, Texture2D pixel, PixelFont font, string digits, Rectangle cells)
    {
        batch.Draw(pixel, cells, Color.Black);
        font.Draw(batch, digits, cells.Location.ToVector2(), Color.Lime, 1);
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
        // SCR-EVENT-001: the researched item starts at frame 0 when the panel opens and after
        // each page change.
        if (LastTurnEventPresentation.ResearchItemId(notification, related) is { } itemId
            && itemId >= 0 && itemId < _itemRotationTextures.Length
            && _itemRotationTextures[itemId] is { } rotation)
            batch.Draw(rotation, LastTurnEventsLayout.ResearchItem,
                ItemRotationPresentation.Frame(
                    _inputTime > _eventPageShownAt ? _inputTime - _eventPageShownAt : TimeSpan.Zero),
                Color.White);
        // SCR-EVENT-001: an elimination report adds the eliminated player's 32-by-32 portrait,
        // stretched to 48 by 48 over its illustration.
        var record = LastTurnEventPresentation.Record(state, notification, related);
        if (record.Type == LastTurnReportRecord.Elimination && _uiSprites is not null
            && state.FindPlayer(new PlayerId(record.Arg1)) is { } eliminated)
            batch.Draw(_uiSprites, LastTurnEventsLayout.EliminatedPortrait,
                OriginalSpriteLayout.OverlordPortrait(eliminated.Setup.PortraitId), Color.White);
    }

    private TimeSpan _eventPageShownAt;

    private static string EventObject(MatchState state, GameNotification notification)
    {
        var related = RelatedEvent(state, notification);
        return LastTurnEventPresentation.Subject(
            state, LastTurnEventPresentation.Record(state, notification, related));
    }

    /// <summary>
    /// This player's reports for the completed turn, memoised for the frame.
    /// </summary>
    /// <remarks>
    /// <c>DrawBoard</c> asks this every frame just to decide whether to blink the Events light, and
    /// the Events screen asks it two or three more times per frame. The answer only changes when an
    /// event is appended, a notification is dismissed, or the turn moves, so those three make up the
    /// key. The state is compared by identity because adopting an online turn replaces the object.
    /// </remarks>
    private IReadOnlyList<GameNotification> LastTurnReports(MatchState state, PlayerId playerId)
    {
        var key = (state.Events.Count, state.Coordinator.Turn, state.NotificationsFor(playerId).Count);
        if (!ReferenceEquals(_lastTurnReportSource, state))
        {
            _lastTurnReportCache.Clear();
            _lastTurnReportSource = state;
        }
        if (_lastTurnReportCache.TryGetValue(playerId, out var cached) && cached.Key == key)
            return cached.Reports;
        var reports = LastTurnEventProjection.For(state, playerId);
        _lastTurnReportCache[playerId] = (key, reports);
        return reports;
    }

    private MatchState? _lastTurnReportSource;

    private readonly Dictionary<
        PlayerId,
        ((int Events, int Turn, int Notifications) Key, IReadOnlyList<GameNotification> Reports)>
        _lastTurnReportCache = [];

    private IReadOnlyList<GameNotification> ReviewableReports(MatchState state, PlayerId playerId)
    {
        var current = LastTurnReports(state, playerId);
        return current.Count > 0
            ? current
            : _lastTurnEventArchive.For(playerId, state.Coordinator.Turn);
    }

    /// <summary>
    /// The event a notification points at.
    /// </summary>
    private static GameEvent? RelatedEvent(MatchState state, GameNotification notification) =>
        notification.RelatedEventSequence is { } sequence
            ? EventBySequence(state.Events, sequence)
            : null;

    /// <summary>
    /// The event carrying <paramref name="sequence"/>, or <c>null</c> when the log has no such event.
    /// </summary>
    /// <remarks>
    /// Binary search rather than a linear scan: the log is append-only in ascending sequence order,
    /// never trimmed, and the screens that ask this redraw every frame. <c>MatchState</c> maintains
    /// that ordering on both append and restore, so every caller shares this one lookup.
    /// </remarks>
    private static GameEvent? EventBySequence(IReadOnlyList<GameEvent> events, long sequence)
    {
        var low = 0;
        var high = events.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var found = events[middle].Sequence;
            if (found == sequence) return events[middle];
            if (found < sequence) low = middle + 1;
            else high = middle - 1;
        }
        return null;
    }

    private static void ClearLastTurnEventFields(SpriteBatch batch, Texture2D pixel)
    {
        // SCR-EVENT-001: the compositor's two black fills behind the subject and the caption.
        batch.Draw(pixel, LastTurnEventsLayout.SubjectBacking, Color.Black);
        batch.Draw(pixel, LastTurnEventsLayout.CaptionBacking, Color.Black);
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

        // The event list is append-only in turn order and is never trimmed, so indexing all of it
        // allocated a dictionary the size of the whole match — several hundred kilobytes by turn
        // 150, onto the large object heap, on every frame that drew the board. A report for the
        // completed turn can only relate to an event from that turn, so the tail is enough.
        var first = events.Count;
        while (first > 0 && events[first - 1].Turn >= completedTurn) first--;
        var eventsBySequence = new Dictionary<long, GameEvent>(events.Count - first);
        for (var index = first; index < events.Count; index++)
            eventsBySequence[events[index].Sequence] = events[index];
        var reports = new List<GameNotification>(MatchLimits.LastTurnReportsPerPlayer);
        var controlledSectors = new HashSet<int?>();
        foreach (var notification in notifications)
        {
            if (notification.Turn != completedTurn) continue;
            var related = notification.RelatedEventSequence is { } sequence
                && eventsBySequence.TryGetValue(sequence, out var gameEvent)
                    ? gameEvent
                    : null;
            if (!NotificationPresentation.IsLastTurnReport(notification, related)) continue;
            // Every gang of the winning player gets a Control result, and the report is one per
            // sector. RULE-EVENT-006 records one report per completed site, so Influence reports
            // are all kept: two sites completed in one sector give two (RULE-EVENT-005).
            if (notification.Kind == GameNotificationKind.Control
                && !controlledSectors.Add(notification.SectorId))
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

/// <summary>
/// One FMT-STATE-006 Last Turn report record: `report_type` and its three arguments.
/// </summary>
public readonly record struct LastTurnReportRecord(int Type, int Arg1, int Arg2, int Arg3)
{
    public const int Crackdown = 1;
    public const int ControlGained = 2;
    public const int ControlLost = 3;
    public const int SiteCompleted = 4;
    public const int ResearchCompleted = 5;
    public const int CashShort = 6;
    public const int HireSectorFull = 7;
    public const int HireRosterFull = 8;
    public const int Elimination = 9;

    /// <summary>`arg1` of a cash report: the order that failed.</summary>
    public const int CashShortBribe = 1;
    public const int CashShortEquip = 2;
    public const int CashShortHire = 4;
}

public static class LastTurnEventPresentation
{
    public const int NativeDitherPatternSize = 8;

    /// <summary>
    /// SCR-EVENT-001: the illustration is resource 6000 plus the report type, for every type from
    /// 1 to 9 (FMT-STATE-006). Types 4 and 5 draw theirs over the site picture and under the
    /// researched item.
    /// </summary>
    public static int ArtworkIndex(GameNotification notification, GameEvent? relatedEvent) =>
        ReportType(notification, relatedEvent);

    /// <summary>
    /// FMT-STATE-006 `report_type` of a report, or 0 for a notification the original does not
    /// record (RULE-EVENT-002).
    /// </summary>
    public static int ReportType(GameNotification notification, GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (IsCashShortCommand(relatedEvent)) return LastTurnReportRecord.CashShort;
        return notification.Kind switch
        {
            GameNotificationKind.Crackdown => LastTurnReportRecord.Crackdown,
            GameNotificationKind.Control => LastTurnReportRecord.ControlGained,
            GameNotificationKind.ControlLost => LastTurnReportRecord.ControlLost,
            GameNotificationKind.Influence => LastTurnReportRecord.SiteCompleted,
            GameNotificationKind.Research => LastTurnReportRecord.ResearchCompleted,
            GameNotificationKind.HireInsufficientCash => LastTurnReportRecord.CashShort,
            GameNotificationKind.HireSectorFull => LastTurnReportRecord.HireSectorFull,
            GameNotificationKind.HireGangLimit => LastTurnReportRecord.HireRosterFull,
            GameNotificationKind.Elimination when relatedEvent?.Kind == GameEventKind.PlayerEliminated =>
                LastTurnReportRecord.Elimination,
            _ => 0
        };
    }

    /// <summary>
    /// The FMT-STATE-006 record the original stores for a report, rebuilt from the notification
    /// and its event. The recording rules fill the arguments: RULE-EVENT-004 (Crackdown),
    /// RULE-EVENT-012 and RULE-EVENT-013 (Control), RULE-EVENT-006 (site), RULE-EVENT-007
    /// (Research), RULE-EVENT-008, RULE-EVENT-014 and RULE-EVENT-009 (cash), RULE-EVENT-010 and
    /// RULE-EVENT-011 (hire) and RULE-EVENT-003 (elimination). The `arg2` of both Control reports
    /// names the other player, which the panel never reads; the rebuild fills it only where its
    /// event carries it and writes -1 otherwise.
    /// </summary>
    public static LastTurnReportRecord Record(
        MatchState state,
        GameNotification notification,
        GameEvent? relatedEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        var type = ReportType(notification, relatedEvent);
        var sector = notification.SectorId ?? -1;
        if (IsCashShortCommand(relatedEvent))
        {
            if (relatedEvent!.Action == GangAction.Bribe)
                return new(type, LastTurnReportRecord.CashShortBribe, sector, 0);
            var definition = relatedEvent.Gang is { } gangId
                && state.FindCombatant(relatedEvent, gangId) is { } gang
                    ? gang.DefinitionId
                    : -1;
            return new(type, LastTurnReportRecord.CashShortEquip, sector, definition);
        }
        return type switch
        {
            LastTurnReportRecord.Crackdown or LastTurnReportRecord.HireSectorFull =>
                new(type, sector, 0, 0),
            LastTurnReportRecord.ControlGained =>
                new(type, sector, relatedEvent?.Resolution?.PreviousValue ?? -1, 0),
            LastTurnReportRecord.ControlLost => new(type, sector, -1, 0),
            LastTurnReportRecord.SiteCompleted
                when InfluenceSiteId(notification, relatedEvent) is { } site =>
                new(type, site / MatchLimits.SitesPerSector, site % MatchLimits.SitesPerSector, 0),
            LastTurnReportRecord.ResearchCompleted
                when ResearchItemId(notification, relatedEvent) is { } item =>
                new(type, item, 0, 0),
            LastTurnReportRecord.CashShort when relatedEvent?.Hire is { } hire =>
                new(type, LastTurnReportRecord.CashShortHire, hire.GangDefinitionId, 0),
            LastTurnReportRecord.HireRosterFull when relatedEvent?.Hire is { } hire =>
                new(type, hire.GangDefinitionId, 0, 0),
            LastTurnReportRecord.Elimination when relatedEvent?.Elimination is { } elimination =>
                new(type, elimination.EliminatedPlayer.Value, 0, 0),
            _ => new(0, 0, 0, 0)
        };
    }

    /// <summary>
    /// SCR-EVENT-001: the subject line of a report, from its FMT-STATE-006 arguments. A sector is
    /// shown by its label, a site report adds the name of the site now in slot `arg2`, and a cash
    /// report for Equip adds the gang's definition name cut to 20 characters.
    /// </summary>
    public static string Subject(MatchState state, LastTurnReportRecord record)
    {
        ArgumentNullException.ThrowIfNull(state);
        return record.Type switch
        {
            LastTurnReportRecord.Crackdown or LastTurnReportRecord.ControlGained
                or LastTurnReportRecord.ControlLost or LastTurnReportRecord.HireSectorFull =>
                SectorLabel(record.Arg1),
            LastTurnReportRecord.SiteCompleted =>
                $"{SectorLabel(record.Arg1)}:{SiteName(state, record.Arg1, record.Arg2)}",
            LastTurnReportRecord.ResearchCompleted => state.Definitions.Items[record.Arg1].Name,
            LastTurnReportRecord.CashShort => record.Arg1 switch
            {
                LastTurnReportRecord.CashShortBribe => SectorLabel(record.Arg2),
                LastTurnReportRecord.CashShortEquip when record.Arg3 >= 0 =>
                    $"{SectorLabel(record.Arg2)}:{Cut(GangName(state, record.Arg3), 20)}",
                LastTurnReportRecord.CashShortEquip => SectorLabel(record.Arg2),
                LastTurnReportRecord.CashShortHire => GangName(state, record.Arg2),
                _ => string.Empty
            },
            LastTurnReportRecord.HireRosterFull => GangName(state, record.Arg1),
            LastTurnReportRecord.Elimination =>
                state.FindPlayer(new PlayerId(record.Arg1))?.Setup.Name.ToUpperInvariant()
                    ?? $"PLAYER {record.Arg1 + 1}",
            _ => string.Empty
        };

        static string SectorLabel(int sector) =>
            sector is >= 0 and < MatchLimits.SectorCount
                ? SectorGangsLayout.SectorCodeText(sector)
                : string.Empty;

        static string SiteName(MatchState state, int sector, int slot) =>
            state.Definitions.Site(state.Sectors[sector].Sites[slot].DefinitionId).Name;

        static string GangName(MatchState state, int definition) =>
            state.Definitions.Gang(checked((short)definition)).Name;

        static string Cut(string value, int length) => value.Length > length ? value[..length] : value;
    }

    private static bool IsCashShortCommand(GameEvent? relatedEvent) =>
        relatedEvent is
        {
            Kind: GameEventKind.CommandFailed,
            Action: GangAction.Bribe or GangAction.Equip,
            Resolution.Code: CommandResolutionCode.InsufficientCash
        };

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

    public static Rectangle SiteBackgroundSource(short definitionId)
    {
        var portrait = OriginalSpriteLayout.SitePortrait(definitionId);
        return new Rectangle(portrait.X + 12, portrait.Y + 1, 94, 62);
    }

    public static bool NativeDitherKeepsPixel(int x, int y)
        => OriginalPatternMask.PreservesDestination(OriginalPatternMask.Sparse, x, y);

    public static Texture2D CreateEventSiteDitherOverlay(GraphicsDevice graphicsDevice) =>
        CreatePatternOverlay(graphicsDevice,
            LastTurnEventsLayout.Artwork.Width, LastTurnEventsLayout.Artwork.Height);

    /// <summary>Black drawn through the sparse pattern over a <paramref name="width"/> by
    /// <paramref name="height"/> area.</summary>
    public static Texture2D CreatePatternOverlay(GraphicsDevice graphicsDevice, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
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

    // One frame per tick of the presentation clock (RULE-UI-008, SCR-UI-006, SCR-EVENT-001), so a
    // full turn of fifteen frames takes 2.5 seconds.
    private static readonly TimeSpan FrameDuration = PresentationClock.Period;

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

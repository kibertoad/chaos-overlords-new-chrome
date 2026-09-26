using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenCombatResults(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultPages(_state, viewer);
        if (pages.Count == 0)
        {
            RejectInput("NO COMBAT RESULTS");
            return;
        }
        _combatSummaryCursor = 0;
        SelectCombatResultPage(_state, viewer, pages[0]);
        _managementReturnScreen = returnScreen;
        _screens.Show(ClientScreen.CombatSummary);
    }

    private void OpenCombatDetail(ClientScreen returnScreen)
    {
        OpenCombatResults(returnScreen);
        if (_screens.Current == ClientScreen.CombatSummary)
            ReplayAllCombatDetail();
    }

    /// <summary>Replays every fight the viewer can see from the completed turn, in order.</summary>
    /// <remarks>
    /// The console's Combat Detail is the whole turn, as the automatic presentation is; D on the
    /// open summary (<see cref="ReplaySelectedCombatDetail"/>) replays only the selected fight.
    /// </remarks>
    private void ReplayAllCombatDetail()
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var clips = CombatAnimationRouting.ForPresentation(
            _state, VisibleCombatEvents(_state, viewer), viewer);
        if (_combatAnimationTextures.Count == 0 || clips.Count == 0)
        {
            RejectInput("COMBAT DETAIL UNAVAILABLE");
            return;
        }
        _combatAnimationPlayer.Clear();
        foreach (var clip in clips) _combatAnimationPlayer.Enqueue(clip);
        _message = string.Empty;
    }

    private void HandleCombatSummaryClick(Point point)
    {
        if (CombatResultsLayout.Ok.Contains(point))
        {
            AcceptInput();
            CloseCombatResults();
            return;
        }
        if (CombatResultsLayout.Previous.Contains(point)) MoveCombatSummary(-1);
        else if (CombatResultsLayout.Next.Contains(point)) MoveCombatSummary(1);
        else SelectCombatSummaryEntry(point);
    }

    private void CloseCombatResults()
    {
        if (_openEventsAfterCombat) FinishAutomaticCombatPresentation();
        else
        {
            _screens.Show(_managementReturnScreen);
            CompletePlanningEntryPresentation();
        }
    }

    private void ReplaySelectedCombatDetail()
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultPages(_state, viewer);
        if (pages.Count == 0)
        {
            RejectInput("NO COMBAT DETAIL AVAILABLE");
            return;
        }
        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var gameEvent = pages[_combatSummaryCursor]
            .SelectedResult(_combatSummaryFocal, _combatSummaryOpponent)?.Event;
        if (gameEvent is null)
        {
            RejectInput("NO COMBAT DETAIL AVAILABLE");
            return;
        }
        // A fight of the viewer's keeps the forces it has in the whole turn's presentation; one the
        // pager shows between other players plays from the phase-start forces. Either plays alone,
        // so it holds its result instead of handing off to a reply that is not queued.
        var turn = VisibleCombatEvents(_state, viewer);
        var clips = CombatAnimationRouting.ForPresentation(
                _state, turn.Any(visible => visible.Sequence == gameEvent.Sequence) ? turn : [gameEvent],
                viewer)
            .Where(clip => clip.EventSequence == gameEvent.Sequence)
            .Select(clip => clip with { HandsOff = false })
            .ToList();
        if (_combatAnimationTextures.Count == 0 || clips.Count == 0)
        {
            RejectInput("COMBAT DETAIL UNAVAILABLE");
            return;
        }
        _combatAnimationPlayer.Clear();
        foreach (var clip in clips) _combatAnimationPlayer.Enqueue(clip);
        _message = string.Empty;
    }

    /// <summary>The completed turn's fights the viewer can see, in event order.</summary>
    private IReadOnlyList<GameEvent> VisibleCombatEvents(MatchState state, PlayerId viewer) =>
        VisibleCombatResults(state, viewer)
            .Where(gameEvent => IsVisibleCombatEvent(state, viewer, gameEvent))
            .OrderBy(gameEvent => gameEvent.Sequence)
            .ToArray();

    /// <summary>
    /// A page step: Previous on the first page and Next on the last play the rejected sound, and
    /// a step that changes the page plays the accepted one and focuses the viewer's first entry
    /// of the new page (SCR-COMBAT-001).
    /// </summary>
    private void MoveCombatSummary(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultPages(_state, viewer);
        var count = pages.Count;
        if (count > 0)
        {
            var next = BoundedPageNavigation.Move(_combatSummaryCursor, count, delta);
            var changed = next != _combatSummaryCursor;
            PlayGeneralSound(AudioRouting.PageNavigationSound(changed));
            _combatSummaryCursor = next;
            if (changed) SelectCombatResultPage(_state, viewer, pages[next]);
        }
    }

    private void DrawCombatResultsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        DrawCombatResultsPanelContent(batch, pixel, font, state);
    }

    private void DrawCombatResultsPanelContent(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _combatResultsBackground, CombatResultsLayout.Panel);
        var viewer = ViewingPlayer(state);
        var pages = CombatResultPages(state, viewer);
        if (pages.Count == 0)
        {
            font.Draw(batch, "NO COMBAT RESULTS",
                CombatResultsLayout.EmptyText.ToVector2(), Color.Lime, 1);
            return;
        }

        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        EnsureCombatResultSelection(state, viewer, page);
        DrawCombatResultPageCounter(batch, pixel, font, _combatSummaryCursor, pages.Count);

        DrawCombatResultSector(batch, pixel, font, state, page);
        var timeline = CombatForceTimeline.For(state, page.Results[0].Event);
        DrawCombatResultForces(batch, pixel, state, timeline,
            page.ForcesFor(viewer, RosterSlots(state, viewer)), enemy: false);
        if (_combatSummaryOpponent is { } opponent)
            DrawCombatResultForces(batch, pixel, state, timeline,
                page.ForcesFor(opponent, RosterSlots(state, opponent)), enemy: true);
        DrawCombatResultOpponents(batch, state, viewer, page);
    }

    /// <summary>
    /// The page number from 1 and the page count as two-cell numbers with leading zeros, and the
    /// arrows, greyed on the first and last page (SCR-COMBAT-001, FND-COMBAT-007).
    /// </summary>
    private void DrawCombatResultPageCounter(
        SpriteBatch batch, Texture2D pixel, PixelFont font, int cursor, int count)
    {
        batch.Draw(pixel, CombatResultsLayout.PageNumber, Color.Black);
        batch.Draw(pixel, CombatResultsLayout.PageCount, Color.Black);
        font.Draw(batch, $"{Math.Min(cursor + 1, 99):00}",
            CombatResultsLayout.PageNumber.Location.ToVector2(), Color.Lime, 1);
        font.Draw(batch, $"{Math.Min(count, 99):00}",
            CombatResultsLayout.PageCount.Location.ToVector2(), Color.Lime, 1);
        if (_uiSprites is null) return;
        batch.Draw(_uiSprites, CombatResultsLayout.Previous,
            CombatResultsLayout.PreviousSource(firstPage: cursor == 0), Color.White);
        batch.Draw(_uiSprites, CombatResultsLayout.Next,
            CombatResultsLayout.NextSource(lastPage: cursor == count - 1), Color.White);
    }

    private IReadOnlyList<GameEvent> VisibleCombatResults(MatchState state, PlayerId viewer) =>
        CombatResultPages(state, viewer).SelectMany(page => page.Results)
            .Select(result => result.Event).ToArray();

    private IReadOnlyList<CombatResultPage> CombatResultPages(MatchState state, PlayerId viewer)
    {
        var key = (state.Events.Count, state.Coordinator.Turn);
        if (!ReferenceEquals(_combatResultSource, state))
        {
            _combatResultCache.Clear();
            _combatResultSource = state;
        }
        if (_combatResultCache.TryGetValue(viewer, out var cached) && cached.Key == key)
            return cached.Pages;
        var pages = CombatResultProjection.Pages(state, viewer);
        _combatResultCache[viewer] = (key, pages);
        return pages;
    }

    private MatchState? _combatResultSource;

    private readonly Dictionary<
        PlayerId,
        ((int Events, int Turn) Key, IReadOnlyList<CombatResultPage> Pages)>
        _combatResultCache = [];

    /// <summary>
    /// The sector tile framed in black, the police strip over its top when the police found a
    /// gang there, and the sector code (SCR-COMBAT-001, FND-COMBAT-007).
    /// </summary>
    private void DrawCombatResultSector(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state, CombatResultPage page)
    {
        var sectorId = page.SectorId;
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatResultsLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        DrawBorder(batch, pixel, CombatResultsLayout.Sector, Color.Black, 1);
        if (page.HasPolice && _uiSprites is not null)
            batch.Draw(_uiSprites, CombatResultsLayout.PoliceStrip,
                CombatResultsLayout.PoliceStripSource, Color.White);
        font.Draw(batch, SectorCode(sectorId),
            CombatResultsLayout.SectorCodeText.ToVector2(), Color.Lime, 1);
    }

    /// <summary>
    /// One player's row of the page: each gang's focus outlines, its portrait scaled to 40 by 40,
    /// and its <c>force_start</c> and <c>force_final</c> tracks (SCR-COMBAT-001, FND-COMBAT-012).
    /// </summary>
    private void DrawCombatResultForces(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        CombatForceTimeline timeline,
        IReadOnlyList<CombatResultForce> forces,
        bool enemy)
    {
        for (var slot = 0; slot < Math.Min(forces.Count, MatchLimits.FriendlyGangsPerSector); slot++)
        {
            var force = forces[slot];
            var gang = state.FindCombatant(force.Event, force.Gang);
            if (gang is null) continue;
            var cell = CombatResultsLayout.Force(slot, enemy);
            var outline = CombatResultsLayout.FocusOutline(cell);
            var marks = CombatResultFocus.Marks(
                force.Gang, force.Target, _combatSummaryFocal, _combatSummaryFocalTarget);
            if (marks.HasFlag(CombatResultOutline.Focal))
                DrawBorder(batch, pixel, outline, CombatResultFocus.FocalColor, 1);
            if (marks.HasFlag(CombatResultOutline.Target))
                DrawBorder(batch, pixel, outline, CombatResultFocus.TargetColor, 1);
            if (marks.HasFlag(CombatResultOutline.Attacker))
                DrawBorder(batch, pixel, outline, CombatResultFocus.AttackerColor, 1);
            if (marks.HasFlag(CombatResultOutline.Mutual) && _uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, outline, CombatResultsLayout.MutualFocusSource, Color.White);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, cell,
                    OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
            DrawForceTrack(batch, pixel, CombatResultsLayout.ForceTrack(cell, 0),
                CombatResultsLayout.ForceTrackFill(timeline.PhaseStartForce(force.Gang)));
            DrawForceTrack(batch, pixel, CombatResultsLayout.ForceTrack(cell, 1),
                CombatResultsLayout.ForceTrackFill(timeline.PhaseFinalForce(force.Gang)));
        }
    }

    /// <summary>
    /// The other five players in player order, dim for a player with no result in the sector,
    /// and the frame around the chosen opponent (SCR-COMBAT-001, FND-COMBAT-007, FND-COMBAT-009).
    /// </summary>
    private void DrawCombatResultOpponents(
        SpriteBatch batch,
        MatchState state,
        PlayerId viewer,
        CombatResultPage page)
    {
        var owners = CombatResultOpponents(state, viewer);
        for (var slot = 0; slot < owners.Count; slot++)
        {
            var player = state.FindPlayer(owners[slot])!;
            var available = page.ForcesFor(player.Id).Count > 0;
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, CombatResultsLayout.Opponent(slot),
                    CombatResultsLayout.OpponentPortraitSource(player.Setup.PortraitId, available),
                    Color.White);
            if (player.Id == _combatSummaryOpponent && _uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, CombatResultsLayout.OpponentFrame(slot),
                    CombatResultsLayout.OpponentFrameSource, Color.White);
        }
    }

    private static IReadOnlyList<PlayerId> CombatResultOpponents(MatchState state, PlayerId viewer) =>
        state.Players.Select(player => player.Id)
            .Where(player => player != viewer).OrderBy(player => player.Value)
            .Take(CombatResultsLayout.OpponentSlots).ToArray();

    /// <summary>The roster slot of each of <paramref name="player"/>'s gangs, the order of its row.</summary>
    private static Func<GangId, int> RosterSlots(MatchState state, PlayerId player)
    {
        var roster = state.FindPlayer(player)?.Gangs ?? [];
        return gang =>
        {
            for (var index = 0; index < roster.Count; index++)
                if (roster[index].Id == gang) return index;
            return int.MaxValue;
        };
    }

    /// <summary>
    /// A click on an opponent portrait or on the viewer's force selector (SCR-COMBAT-001). A
    /// portrait is accepted only for a player with a result in the sector, and choosing another
    /// opponent plays the accepted sound. The selector makes the slot's gang the focal gang and its
    /// target the focal target, and keeps the opponent (FND-COMBAT-012).
    /// </summary>
    private void SelectCombatSummaryEntry(Point point)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultPages(_state, viewer);
        if (pages.Count == 0) return;
        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        var opponents = CombatResultOpponents(_state, viewer);
        for (var opponentSlot = 0; opponentSlot < opponents.Count; opponentSlot++)
        {
            if (!CombatResultsLayout.Opponent(opponentSlot).Contains(point)) continue;
            var opponent = opponents[opponentSlot];
            if (_combatSummaryOpponent == opponent || page.ForcesFor(opponent).Count == 0) return;
            AcceptInput();
            _combatSummaryOpponent = opponent;
            return;
        }
        var viewerForces = page.ForcesFor(viewer, RosterSlots(_state, viewer));
        if (CombatResultsLayout.FriendlyForceSlotAt(point) is { } slot && slot < viewerForces.Count)
        {
            _combatSummaryFocal = viewerForces[slot].Gang;
            _combatSummaryFocalTarget = viewerForces[slot].Target;
        }
    }

    /// <summary>Reselects the page's opening focus when the results under the panel changed.</summary>
    private void EnsureCombatResultSelection(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        if (_combatSummarySector != page.SectorId
            || _combatSummaryFocal is { } focal
            && page.ForcesFor(viewer).All(force => force.Gang != focal)
            || _combatSummaryOpponent is { } opponent && page.ForcesFor(opponent).Count == 0)
            SelectCombatResultPage(state, viewer, page);
    }

    /// <summary>
    /// A page as it opens: the viewer's first entry is the focal gang, and the first opponent in
    /// player order with a result is chosen (SCR-COMBAT-001, FND-COMBAT-007, FND-COMBAT-012).
    /// </summary>
    private void SelectCombatResultPage(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        _combatSummarySector = page.SectorId;
        var first = page.ForcesFor(viewer, RosterSlots(state, viewer)).FirstOrDefault();
        _combatSummaryFocal = first?.Gang;
        _combatSummaryFocalTarget = first?.Target;
        _combatSummaryOpponent = CombatResultOpponents(state, viewer)
            .Cast<PlayerId?>()
            .FirstOrDefault(opponent => page.ForcesFor(opponent!.Value).Count > 0);
    }
}

/// <summary>The outlines a Combat Results grid cell gets from the focal gang (FND-COMBAT-012).</summary>
[Flags]
public enum CombatResultOutline
{
    None = 0,
    /// <summary>The focal gang, outlined in green.</summary>
    Focal = 1,
    /// <summary>The focal gang's target, outlined in red.</summary>
    Target = 2,
    /// <summary>A gang that attacked the focal gang, outlined in yellow.</summary>
    Attacker = 4,
    /// <summary>The focal gang's target that also attacked it, marked with the art at <c>(468,15)</c>.</summary>
    Mutual = 8
}

/// <summary>The focus marks of SCR-COMBAT-001 (FND-COMBAT-012, FND-COMBAT-013).</summary>
public static class CombatResultFocus
{
    public static readonly Color FocalColor = new(0, 255, 0);
    public static readonly Color TargetColor = new(255, 0, 0);
    public static readonly Color AttackerColor = new(255, 255, 0);

    /// <summary>
    /// The marks of the cell of <paramref name="gang"/>, whose own attack target is
    /// <paramref name="target"/>, drawn in the order of the flags. Nothing is marked while no
    /// focal gang is set.
    /// </summary>
    public static CombatResultOutline Marks(GangId gang, GangId? target, GangId? focal, GangId? focalTarget)
    {
        if (focal is not { } focus) return CombatResultOutline.None;
        var marks = CombatResultOutline.None;
        if (gang == focus) marks |= CombatResultOutline.Focal;
        if (gang == focalTarget) marks |= CombatResultOutline.Target;
        if (target == focus)
            marks |= gang == focalTarget ? CombatResultOutline.Mutual : CombatResultOutline.Attacker;
        return marks;
    }
}

public static class CombatResultProjection
{
    public static IReadOnlyList<CombatResultPage> Pages(MatchState state, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var completedTurn = state.Coordinator.Turn - 1;
        var first = state.Events.Count;
        while (first > 0 && state.Events[first - 1].Turn >= completedTurn) first--;
        var entries = new List<CombatResultEntry>();
        for (var index = first; index < state.Events.Count; index++)
        {
            var gameEvent = state.Events[index];
            if (gameEvent.Turn != completedTurn || Describe(state, gameEvent) is not { } entry) continue;
            entries.Add(entry);
        }
        var occupied = state.FindPlayer(viewer)?.Gangs
            .Where(gang => gang.IsActive)
            .Select(gang => gang.SectorId)
            .ToHashSet() ?? [];
        return Pages(entries, viewer, occupied);
    }

    public static IReadOnlyList<CombatResultPage> Pages(
        IEnumerable<CombatResultEntry> entries,
        PlayerId viewer,
        IReadOnlySet<int> occupiedSectors)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(occupiedSectors);
        return entries.OrderBy(entry => entry.Event.Sequence)
            .GroupBy(entry => entry.SectorId)
            .Where(group => occupiedSectors.Contains(group.Key)
                || group.Any(entry => entry.Involves(viewer)))
            .OrderBy(group => group.Key)
            .Select(group => new CombatResultPage(group.Key, group.ToArray()))
            .ToArray();
    }

    /// <summary>
    /// The sequence of the last event recorded before <paramref name="turn"/>, or -1 when the
    /// history starts at or after it.
    /// </summary>
    /// <remarks>
    /// Walks back from the end, as the list is append-only in turn order, so the cost is the
    /// events of the turns being skipped rather than the whole match.
    /// </remarks>
    public static long LastSequenceBefore(IReadOnlyList<GameEvent> events, int turn)
    {
        ArgumentNullException.ThrowIfNull(events);
        var first = events.Count;
        while (first > 0 && events[first - 1].Turn >= turn) first--;
        return first > 0 ? events[first - 1].Sequence : -1;
    }

    public static bool IsFromLastCompletedTurn(int eventTurn, int currentTurn)
    {
        if (eventTurn < 0) throw new ArgumentOutOfRangeException(nameof(eventTurn));
        if (currentTurn < 1) throw new ArgumentOutOfRangeException(nameof(currentTurn));
        return eventTurn == currentTurn - 1;
    }

    private static CombatResultEntry? Describe(MatchState state, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            return DescribePolice(state, gameEvent);
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution is null
            || gameEvent.Gang is not { } attackerId
            || gameEvent.Target.Kind != CommandTargetKind.Gang
            || state.FindCombatant(gameEvent, attackerId) is not { } attacker
            || state.FindCombatant(gameEvent, new GangId(gameEvent.Target.Id)) is not { } defender)
            return null;
        return new CombatResultEntry(
            gameEvent, attacker.SectorId, attacker.Owner, attacker.Id,
            defender.Owner, defender.Id, false);
    }

    /// <summary>The police encounter a detected gang suffered, or nothing for an undetected one.</summary>
    /// <remarks>
    /// Every gang in a crackdown sector gets a police detection roll, including a gang that leaves
    /// in the same turn's Movement. A failed roll is recorded for the simulation, but the police
    /// never engaged that gang and the original has no presentation for it. Listing it would draw
    /// the police against a gang they did not attack, give Combat Summary a page with nothing to
    /// replay, and open the automatic combat presentation for a turn without any combat.
    /// </remarks>
    private static CombatResultEntry? DescribePolice(MatchState state, GameEvent gameEvent) =>
        gameEvent is { Gang: { } policeTarget, PoliceAttack: { Detected: true } police }
        && state.FindCombatant(gameEvent, policeTarget) is { } target
            ? new CombatResultEntry(
                gameEvent, police.SectorId, target.Owner, target.Id, null, null, true)
            : null;
}

public sealed record CombatResultEntry(
    GameEvent Event,
    int SectorId,
    PlayerId FirstPlayer,
    GangId FirstGang,
    PlayerId? SecondPlayer,
    GangId? SecondGang,
    bool Police)
{
    public bool Involves(PlayerId player) => FirstPlayer == player || SecondPlayer == player;

    public GangId? GangFor(PlayerId player) => FirstPlayer == player
        ? FirstGang
        : SecondPlayer == player ? SecondGang : null;
}

/// <summary>
/// One entry of a player's row in a sector: a gang that fought there, the event it is found by,
/// and its own Attack target, or null when it did not attack (RULE-COMBAT-002).
/// </summary>
public sealed record CombatResultForce(GangId Gang, GameEvent Event, GangId? Target = null);

public sealed record CombatResultPage(int SectorId, IReadOnlyList<CombatResultEntry> Results)
{
    /// <summary>Whether the police found a gang in the sector, which sets a police flag (RULE-POLICE-001).</summary>
    public bool HasPolice => Results.Any(result => result.Police);

    /// <summary>
    /// <paramref name="player"/>'s row of the sector: each of its gangs that fought there, at most
    /// six, with its own Attack target. The resolver fills the row in roster order
    /// (RULE-COMBAT-002), which <paramref name="rosterSlot"/> gives; without it the gangs keep the
    /// order they first appear in the results.
    /// </summary>
    public IReadOnlyList<CombatResultForce> ForcesFor(
        PlayerId player,
        Func<GangId, int>? rosterSlot = null)
    {
        var forces = Results
            .Select(result => (Result: result, Gang: result.GangFor(player)))
            .Where(value => value.Gang is not null)
            .GroupBy(value => value.Gang!.Value)
            .Select(group => new CombatResultForce(group.Key, group.First().Result.Event,
                Results.FirstOrDefault(result => !result.Police && result.FirstGang == group.Key)
                    ?.SecondGang));
        if (rosterSlot is not null)
            forces = forces.OrderBy(force => rosterSlot(force.Gang)).ThenBy(force => force.Gang.Value);
        return forces.Take(MatchLimits.FriendlyGangsPerSector).ToArray();
    }

    /// <summary>
    /// The fight the Detail control replays (DEV-COMBAT-002): the focal gang's own attack, else a
    /// fight it was in, else the first fight of <paramref name="opponent"/>, else the page's first.
    /// </summary>
    public CombatResultEntry? SelectedResult(GangId? focal, PlayerId? opponent) =>
        Results.FirstOrDefault(result => !result.Police && result.FirstGang == focal)
        ?? Results.FirstOrDefault(result => focal is { } gang
            && (result.FirstGang == gang || result.SecondGang == gang))
        ?? Results.FirstOrDefault(result => opponent is { } other && result.Involves(other))
        ?? Results.FirstOrDefault();
}

public sealed class CombatPresentationProgress
{
    private readonly Dictionary<PlayerId, long> _lastSeen = [];

    public long LastSeen(PlayerId player) => _lastSeen.GetValueOrDefault(player, -1);

    public void MarkSeen(PlayerId player, long sequence)
    {
        if (sequence < -1 || sequence < LastSeen(player))
            throw new ArgumentOutOfRangeException(nameof(sequence));
        _lastSeen[player] = sequence;
    }

    public void ResetTo(IEnumerable<PlayerId> players, long sequence)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (sequence < -1) throw new ArgumentOutOfRangeException(nameof(sequence));
        _lastSeen.Clear();
        foreach (var player in players) _lastSeen[player] = sequence;
    }

    public void Clear() => _lastSeen.Clear();
}

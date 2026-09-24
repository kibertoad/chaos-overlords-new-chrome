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
        SelectDefaultCombatResult(_state, viewer, pages[0]);
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
        var events = VisibleCombatResults(_state, viewer)
            .Where(gameEvent => IsVisibleCombatEvent(_state, viewer, gameEvent))
            .OrderBy(gameEvent => gameEvent.Sequence);
        var clips = events.SelectMany(gameEvent => CombatClipsOrNone(_state, gameEvent, viewer)).ToArray();
        if (_combatAnimationTextures.Count == 0 || clips.Length == 0)
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
        var gameEvent = SelectedCombatResult(pages[_combatSummaryCursor]);
        if (gameEvent is null)
        {
            RejectInput("NO COMBAT DETAIL AVAILABLE");
            return;
        }
        var clips = CombatClipsOrNone(_state, gameEvent, viewer);
        if (_combatAnimationTextures.Count == 0 || clips.Count == 0)
        {
            RejectInput("COMBAT DETAIL UNAVAILABLE");
            return;
        }
        _combatAnimationPlayer.Clear();
        foreach (var clip in clips) _combatAnimationPlayer.Enqueue(clip);
        _message = string.Empty;
    }

    /// <summary>The clips for one combat event, or none when the event cannot be drawn.</summary>
    /// <remarks>
    /// Routing already omits a fight whose gangs neither the state nor the event can name, but it
    /// still throws for an item id outside this state's definitions, which a client-side divergence
    /// can pair with an event. Detailed combat is optional presentation, so every path that plays
    /// it omits such an event instead of crashing the game over it.
    /// </remarks>
    private static IReadOnlyList<CombatAnimationClip> CombatClipsOrNone(
        MatchState state,
        GameEvent gameEvent,
        PlayerId viewer)
    {
        try
        {
            return CombatAnimationRouting.ForEvent(state, gameEvent, viewer);
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

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
            if (changed) SelectDefaultCombatResult(_state, viewer, pages[next]);
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
        ClearCombatResultPage(batch, pixel);
        if (pages.Count == 0)
        {
            font.Draw(batch, "NO COMBAT RESULTS",
                CombatResultsLayout.EmptyText.ToVector2(), Color.Lime, 1);
            return;
        }

        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        EnsureCombatResultSelection(state, viewer, page);
        font.Draw(batch, $"{_combatSummaryCursor + 1:00} OF {pages.Count:00}",
            CombatResultsLayout.PageText.ToVector2(), Color.Lime, 1);

        DrawCombatResultSector(batch, font, state, page.SectorId);
        DrawCombatResultForces(batch, pixel, state, page.ForcesFor(viewer), enemy: false);
        if (SelectedCombatResult(page)?.PoliceAttack is not null)
            DrawCombatResultPolice(batch, pixel, CombatResultsLayout.Force(0, enemy: true));
        else if (_combatSummaryOpponent is { } opponent)
            DrawCombatResultForces(batch, pixel, state, page.ForcesFor(opponent), enemy: true);
        DrawCombatResultOpponents(batch, pixel, state, viewer, page);
        DrawButton(batch, pixel, font, CombatResultsLayout.Ok, "OK", true);
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

    private void DrawCombatResultSector(SpriteBatch batch, PixelFont font, MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatResultsLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        font.Draw(batch, SectorCode(sectorId),
            CombatResultsLayout.SectorCodeText.ToVector2(), Color.Lime, 1);
    }

    private void DrawCombatResultForces(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        IReadOnlyList<CombatResultForce> forces,
        bool enemy)
    {
        for (var slot = 0; slot < Math.Min(forces.Count, MatchLimits.FriendlyGangsPerSector); slot++)
        {
            var force = forces[slot];
            var gang = state.FindCombatant(force.Event, force.Gang);
            if (gang is null) continue;
            var cell = CombatResultsLayout.Force(slot, enemy);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, cell,
                    OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
            DrawBorder(batch, pixel, cell, PlayerColors[gang.Owner.Value],
                force.Event.Sequence == _combatSummaryEventSequence ? 2 : 1);
            DrawCombatForce(batch, pixel, CombatResultsLayout.ForceBar(cell), gang.Force);
        }
    }

    private void DrawCombatResultPolice(SpriteBatch batch, Texture2D pixel, Rectangle panel)
    {
        batch.Draw(pixel, panel, Color.Black);
        if (_policeSprites is not null)
            batch.Draw(_policeSprites, panel,
                OriginalSpriteLayout.PolicePatrolCar, Color.White);
        DrawBorder(batch, pixel, panel, Color.LightBlue, 2);
        DrawCombatForce(batch, pixel, CombatResultsLayout.ForceBar(panel),
            ManualRules.MaximumForce);
    }

    private void DrawCombatResultOpponents(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        PlayerId viewer,
        CombatResultPage page)
    {
        var owners = state.Players.Select(player => player.Id)
            .Where(player => player != viewer).OrderBy(player => player.Value)
            .Take(CombatResultsLayout.OpponentSlots).ToArray();
        for (var slot = 0; slot < owners.Length; slot++)
        {
            var player = state.FindPlayer(owners[slot])!;
            var destination = CombatResultsLayout.Opponent(slot);
            var available = page.ForcesFor(player.Id).Count > 0;
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, destination,
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId),
                    available ? Color.White : new Color(48, 48, 48));
            DrawBorder(batch, pixel, destination, PlayerColors[player.Id.Value],
                player.Id == _combatSummaryOpponent ? 2 : 1);
        }
    }

    private void SelectCombatSummaryEntry(Point point)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultPages(_state, viewer);
        if (pages.Count == 0) return;
        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        var opponents = _state.Players.Select(player => player.Id)
            .Where(player => player != viewer).OrderBy(player => player.Value)
            .Take(CombatResultsLayout.OpponentSlots).ToArray();
        for (var opponentSlot = 0; opponentSlot < opponents.Length; opponentSlot++)
        {
            if (!CombatResultsLayout.Opponent(opponentSlot).Contains(point)) continue;
            var opponent = opponents[opponentSlot];
            if (_combatSummaryOpponent == opponent
                || page.ResultAgainst(viewer, opponent) is not { } selected) return;
            AcceptInput();
            // The clicked player, not one derived from the fight: in a fight the viewer is not in,
            // the other side is whoever happened to be listed first, and that may not be them.
            SelectCombatResult(selected, opponent);
            return;
        }
        var viewerForces = page.ForcesFor(viewer);
        var forceSlot = CombatResultsLayout.FriendlyForceSlotAt(point);
        if (forceSlot is { } slot && slot < viewerForces.Count)
        {
            var selected = page.Results.First(result =>
                result.Event.Sequence == viewerForces[slot].Event.Sequence);
            SelectCombatResult(selected, selected.OpponentFor(viewer));
        }
    }

    private void EnsureCombatResultSelection(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        if (page.Results.All(result => result.Event.Sequence != _combatSummaryEventSequence))
            SelectDefaultCombatResult(state, viewer, page);
    }

    private void SelectDefaultCombatResult(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        var selected = page.Results.FirstOrDefault(result => result.Involves(viewer))
            ?? page.Results[0];
        SelectCombatResult(selected, selected.OpponentFor(viewer));
    }

    private void SelectCombatResult(CombatResultEntry selected, PlayerId? opponent)
    {
        _combatSummaryEventSequence = selected.Event.Sequence;
        _combatSummaryOpponent = opponent;
    }

    private GameEvent? SelectedCombatResult(CombatResultPage page) => page.Results
        .FirstOrDefault(result => result.Event.Sequence == _combatSummaryEventSequence)?.Event;

    private static void ClearCombatResultPage(SpriteBatch batch, Texture2D pixel)
        => batch.Draw(pixel, CombatResultsLayout.Page, Color.Black);
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

    /// <summary>The side of this result facing <paramref name="player"/>.</summary>
    /// <remarks>
    /// For a result <paramref name="player"/> is not in, there is no side facing them, and the
    /// first-listed player stands in so the page still shows one side of the fight. A caller that
    /// knows which of the two it wants, such as a click on a portrait, passes that player instead.
    /// </remarks>
    public PlayerId? OpponentFor(PlayerId player) => FirstPlayer == player
        ? SecondPlayer
        : SecondPlayer == player ? FirstPlayer : FirstPlayer;

    public GangId? GangFor(PlayerId player) => FirstPlayer == player
        ? FirstGang
        : SecondPlayer == player ? SecondGang : null;
}

public sealed record CombatResultForce(GangId Gang, GameEvent Event);

public sealed record CombatResultPage(int SectorId, IReadOnlyList<CombatResultEntry> Results)
{
    /// <summary>
    /// The fight to show against <paramref name="opponent"/>: the first one they fought
    /// <paramref name="viewer"/> in, else the first one they fought in at all.
    /// </summary>
    public CombatResultEntry? ResultAgainst(PlayerId viewer, PlayerId opponent) =>
        Results.FirstOrDefault(result => result.Involves(opponent) && result.Involves(viewer))
        ?? Results.FirstOrDefault(result => result.Involves(opponent));

    public IReadOnlyList<CombatResultForce> ForcesFor(PlayerId player) => Results
        .Select(result => (Result: result, Gang: result.GangFor(player)))
        .Where(value => value.Gang is not null)
        .GroupBy(value => value.Gang!.Value)
        .Select(group => new CombatResultForce(group.Key, group.First().Result.Event))
        .Take(MatchLimits.FriendlyGangsPerSector)
        .ToArray();
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

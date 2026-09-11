using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenCombatResults(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultProjection.Pages(_state, viewer);
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

    private void HandleCombatSummaryClick(Point point)
    {
        if (CombatResultsLayout.Ok.Contains(point))
        {
            AcceptInput();
            _screens.Show(_managementReturnScreen);
            return;
        }
        if (CombatResultsLayout.Detail.Contains(point))
        {
            ReplaySelectedCombatDetail();
            return;
        }
        if (CombatResultsLayout.Previous.Contains(point)) MoveCombatSummary(-1);
        else if (CombatResultsLayout.Next.Contains(point)) MoveCombatSummary(1);
        else SelectCombatSummaryEntry(point);
    }

    private void ReplaySelectedCombatDetail()
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultProjection.Pages(_state, viewer);
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
        IReadOnlyList<CombatAnimationClip> clips;
        try
        {
            clips = CombatAnimationRouting.ForEvent(_state, gameEvent);
        }
        catch (ArgumentOutOfRangeException)
        {
            clips = [];
        }
        if (_combatAnimationTextures.Count == 0 || clips.Count == 0)
        {
            RejectInput("COMBAT DETAIL UNAVAILABLE");
            return;
        }
        _combatAnimationPlayer.Clear();
        foreach (var clip in clips) _combatAnimationPlayer.Enqueue(clip);
        _message = string.Empty;
    }

    private void MoveCombatSummary(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var pages = CombatResultProjection.Pages(_state, viewer);
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
        if (_combatResultsBackground is not null)
            batch.Draw(_combatResultsBackground, CombatResultsLayout.Panel, Color.White);
        else
            batch.Draw(pixel, CombatResultsLayout.Panel, new Color(0, 0, 0, 245));
        var viewer = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var pages = CombatResultProjection.Pages(state, viewer);
        ClearCombatResultPage(batch, pixel);
        if (pages.Count == 0)
        {
            font.Draw(batch, "NO COMBAT RESULTS", new Vector2(219, 225), Color.Lime, 1);
            return;
        }

        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        EnsureCombatResultSelection(state, viewer, page);
        font.Draw(batch, $"{_combatSummaryCursor + 1:00} OF {pages.Count:00}",
            new Vector2(136, 138), Color.Lime, 1);

        DrawCombatResultSector(batch, font, state, page.SectorId);
        DrawCombatResultForces(batch, pixel, state, page.ForcesFor(viewer), enemy: false);
        if (SelectedCombatResult(page)?.PoliceAttack is not null)
            DrawCombatResultPolice(batch, pixel, CombatResultsLayout.Force(0, enemy: true));
        else if (_combatSummaryOpponent is { } opponent)
            DrawCombatResultForces(batch, pixel, state, page.ForcesFor(opponent), enemy: true);
        DrawCombatResultOpponents(batch, pixel, state, viewer, page);
        DrawButton(batch, pixel, font, CombatResultsLayout.Detail, "DETAIL", true);
    }

    private static IReadOnlyList<GameEvent> VisibleCombatResults(MatchState state, PlayerId viewer) =>
        CombatResultProjection.Pages(state, viewer).SelectMany(page => page.Results)
            .Select(result => result.Event).ToArray();

    private void DrawCombatResultSector(SpriteBatch batch, PixelFont font, MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatResultsLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        font.Draw(batch, SectorCode(sectorId), new Vector2(148, 247), Color.Lime, 1);
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
            var gang = state.FindGang(force.Gang);
            if (gang is null) continue;
            var cell = CombatResultsLayout.Force(slot, enemy);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, cell,
                    OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
            DrawBorder(batch, pixel, cell, PlayerColors[gang.Owner.Value],
                force.Event.Sequence == _combatSummaryEventSequence ? 2 : 1);
            DrawCombatForce(batch, pixel,
                new Rectangle(cell.X, cell.Bottom - 3, cell.Width, 3), gang.Force);
        }
    }

    private void DrawCombatResultPolice(SpriteBatch batch, Texture2D pixel, Rectangle panel)
    {
        batch.Draw(pixel, panel, Color.Black);
        if (_policeSprites is not null)
            batch.Draw(_policeSprites, panel,
                OriginalSpriteLayout.PolicePatrolCar, Color.White);
        DrawBorder(batch, pixel, panel, Color.LightBlue, 2);
        DrawCombatForce(batch, pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3),
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
            .Where(player => player != viewer).OrderBy(player => player.Value).Take(5).ToArray();
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
        var pages = CombatResultProjection.Pages(_state, viewer);
        if (pages.Count == 0) return;
        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, pages.Count - 1);
        var page = pages[_combatSummaryCursor];
        var opponents = _state.Players.Select(player => player.Id)
            .Where(player => player != viewer).OrderBy(player => player.Value).Take(5).ToArray();
        for (var slot = 0; slot < opponents.Length; slot++)
        {
            if (!CombatResultsLayout.Opponent(slot).Contains(point)) continue;
            var forces = page.ForcesFor(opponents[slot]);
            if (forces.Count == 0 || _combatSummaryOpponent == opponents[slot]) return;
            AcceptInput();
            _combatSummaryOpponent = opponents[slot];
            _combatSummaryEventSequence = forces[0].Event.Sequence;
            return;
        }
        var viewerForces = page.ForcesFor(viewer);
        for (var slot = 0; slot < viewerForces.Count; slot++)
        {
            if (!CombatResultsLayout.Force(slot, enemy: false).Contains(point)) continue;
            _combatSummaryEventSequence = viewerForces[slot].Event.Sequence;
            return;
        }
    }

    private void EnsureCombatResultSelection(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        if (page.Results.All(result => result.Event.Sequence != _combatSummaryEventSequence))
            SelectDefaultCombatResult(state, viewer, page);
    }

    private void SelectDefaultCombatResult(MatchState state, PlayerId viewer, CombatResultPage page)
    {
        _combatSummaryEventSequence = page.ForcesFor(viewer).FirstOrDefault()?.Event.Sequence
            ?? page.Results[0].Event.Sequence;
        _combatSummaryOpponent = null;
        foreach (var player in state.Players.Select(candidate => candidate.Id)
                     .Where(player => player != viewer).OrderBy(player => player.Value))
        {
            if (page.ForcesFor(player).Count == 0) continue;
            _combatSummaryOpponent = player;
            break;
        }
    }

    private GameEvent? SelectedCombatResult(CombatResultPage page) => page.Results
        .FirstOrDefault(result => result.Event.Sequence == _combatSummaryEventSequence)?.Event;

    private static void ClearCombatResultPage(SpriteBatch batch, Texture2D pixel)
        => batch.Draw(pixel, new Rectangle(133, 136, 58, 12), Color.Black);
}

public static class CombatResultProjection
{
    public static IReadOnlyList<CombatResultPage> Pages(MatchState state, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var entries = state.Events
            .Where(gameEvent => IsFromLastCompletedTurn(gameEvent.Turn, state.Coordinator.Turn))
            .Select(gameEvent => Describe(state, gameEvent))
            .Where(entry => entry is not null)
            .Cast<CombatResultEntry>()
            .ToArray();
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

    public static bool IsFromLastCompletedTurn(int eventTurn, int currentTurn)
    {
        if (eventTurn < 0) throw new ArgumentOutOfRangeException(nameof(eventTurn));
        if (currentTurn < 1) throw new ArgumentOutOfRangeException(nameof(currentTurn));
        return eventTurn == currentTurn - 1;
    }

    private static CombatResultEntry? Describe(MatchState state, GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.Gang is { } policeTarget
            && state.FindGang(policeTarget) is { } target
            && gameEvent.PoliceAttack is { } police)
            return new CombatResultEntry(
                gameEvent, police.SectorId, target.Owner, target.Id, null, null, true);
        if (gameEvent.Action != GangAction.Attack || gameEvent.Resolution is null
            || gameEvent.Gang is not { } attackerId
            || gameEvent.Target.Kind != CommandTargetKind.Gang
            || state.FindGang(attackerId) is not { } attacker
            || state.FindGang(new GangId(gameEvent.Target.Id)) is not { } defender)
            return null;
        return new CombatResultEntry(
            gameEvent, attacker.SectorId, attacker.Owner, attacker.Id,
            defender.Owner, defender.Id, false);
    }
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

public sealed record CombatResultForce(GangId Gang, GameEvent Event);

public sealed record CombatResultPage(int SectorId, IReadOnlyList<CombatResultEntry> Results)
{
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenCombatResults(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        if (VisibleCombatResults(_state, viewer).Count == 0)
        {
            _message = "NO COMBAT RESULTS";
            return;
        }
        _combatSummaryCursor = 0;
        _managementReturnScreen = returnScreen;
        _screens.Show(ClientScreen.CombatSummary);
    }

    private void HandleCombatSummaryClick(Point point)
    {
        if (CombatResultsLayout.Ok.Contains(point))
        {
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
    }

    private void ReplaySelectedCombatDetail()
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var events = VisibleCombatResults(_state, viewer);
        if (events.Count == 0)
        {
            _message = "NO COMBAT DETAIL AVAILABLE";
            return;
        }
        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, events.Count - 1);
        IReadOnlyList<CombatAnimationClip> clips;
        try
        {
            clips = CombatAnimationRouting.ForEvent(_state, events[_combatSummaryCursor]);
        }
        catch (ArgumentOutOfRangeException)
        {
            clips = [];
        }
        if (_combatAnimationTextures.Count == 0 || clips.Count == 0)
        {
            _message = "COMBAT DETAIL UNAVAILABLE";
            return;
        }
        _combatAnimationPlayer.Clear();
        foreach (var clip in clips) _combatAnimationPlayer.Enqueue(clip);
        _message = "ESCAPE OR CANCEL SKIPS COMBAT DETAIL";
    }

    private void MoveCombatSummary(int delta)
    {
        if (_state?.Coordinator.ActivePlayer is not { } viewer) return;
        var count = VisibleCombatResults(_state, viewer).Count;
        if (count > 0) _combatSummaryCursor = Mod(_combatSummaryCursor + delta, count);
    }

    private void DrawCombatResultsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawBoard(batch, pixel, font, state);
        if (_combatResultsBackground is not null)
            batch.Draw(_combatResultsBackground, CombatResultsLayout.Panel, Color.White);
        else
            batch.Draw(pixel, CombatResultsLayout.Panel, new Color(0, 0, 0, 245));
        var viewer = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var events = VisibleCombatResults(state, viewer);
        ClearCombatResultPage(batch, pixel);
        if (events.Count == 0)
        {
            font.Draw(batch, "NO COMBAT RESULTS", new Vector2(219, 225), Color.Lime, 1);
            return;
        }

        _combatSummaryCursor = Math.Clamp(_combatSummaryCursor, 0, events.Count - 1);
        var gameEvent = events[_combatSummaryCursor];
        font.Draw(batch, $"{_combatSummaryCursor + 1:00} OF {events.Count:00}",
            new Vector2(136, 138), Color.Lime, 1);

        var (friendly, enemy, policeEnemy) = CombatSides(state, viewer, gameEvent);
        var sectorId = friendly?.SectorId ?? enemy?.SectorId ?? gameEvent.PoliceAttack?.SectorId ?? 0;
        DrawCombatResultSector(batch, font, state, sectorId);
        if (friendly is not null)
            DrawCombatResultGang(batch, pixel, font, state, friendly,
                CombatResultsLayout.FriendlyPanel, true);
        if (policeEnemy)
            DrawCombatResultPolice(batch, pixel, font, CombatResultsLayout.EnemyPanel);
        else if (enemy is not null)
            DrawCombatResultGang(batch, pixel, font, state, enemy,
                CombatResultsLayout.EnemyPanel, false);
        DrawCombatResultOpponents(batch, pixel, state, viewer, events, gameEvent, policeEnemy);
        DrawButton(batch, pixel, font, CombatResultsLayout.Detail, "DETAIL", true);
    }

    private static IReadOnlyList<GameEvent> VisibleCombatResults(MatchState state, PlayerId viewer) => state.Events
        .Where(gameEvent => CombatResultProjection.IsFromLastCompletedTurn(
            gameEvent.Turn, state.Coordinator.Turn))
        .Where(gameEvent => IsVisibleCombatEvent(state, viewer, gameEvent))
        .Where(gameEvent => gameEvent.Kind == GameEventKind.PoliceAttackResolved
            || gameEvent.Action == GangAction.Attack)
        .OrderByDescending(gameEvent => gameEvent.Sequence)
        .ToArray();

    private static (MatchGangState? Friendly, MatchGangState? Enemy, bool PoliceEnemy) CombatSides(
        MatchState state,
        PlayerId viewer,
        GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            return (gameEvent.Gang is { } target ? state.FindGang(target) : null, null, true);
        var attacker = gameEvent.Gang is { } attackerId ? state.FindGang(attackerId) : null;
        var defender = gameEvent.Target.Kind == CommandTargetKind.Gang
            ? state.FindGang(new GangId(gameEvent.Target.Id))
            : null;
        return defender?.Owner == viewer
            ? (defender, attacker, false)
            : (attacker, defender, false);
    }

    private void DrawCombatResultSector(SpriteBatch batch, PixelFont font, MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
        if (layer is not null)
            batch.Draw(layer, CombatResultsLayout.Sector, CityMapLayout.Source(sectorId), Color.White);
        font.Draw(batch, SectorCode(sectorId), new Vector2(148, 241), Color.Lime, 1);
    }

    private void DrawCombatResultGang(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state,
        MatchGangState gang,
        Rectangle panel,
        bool friendly)
    {
        var portrait = new Rectangle(panel.X + (panel.Width - 64) / 2, panel.Y + 23, 64, 64);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        DrawBorder(batch, pixel, portrait, PlayerColors[gang.Owner.Value], 1);
        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        var name = definition.Name.Length > 14 ? definition.Name[..14] : definition.Name;
        font.Draw(batch, name, new Vector2(panel.X + 5, panel.Y + 7),
            friendly ? Color.Lime : Color.OrangeRed, 1);
        DrawCombatForce(batch, pixel, new Rectangle(portrait.X, portrait.Bottom + 2, 64, 3), gang.Force);
        DrawCombatItem(batch, gang.WeaponItemId, panel.X + 20, panel.Y + 117);
        DrawCombatItem(batch, gang.ArmorItemId, panel.X + 48, panel.Y + 117);
        DrawCombatItem(batch, gang.MiscellaneousItemId, panel.X + 76, panel.Y + 117);
        font.Draw(batch, $"FORCE {gang.Force}", new Vector2(panel.X + 8, panel.Y + 151), Color.Lime, 1);
    }

    private void DrawCombatResultPolice(SpriteBatch batch, Texture2D pixel, PixelFont font, Rectangle panel)
    {
        font.Draw(batch, "POLICE", new Vector2(panel.X + 7, panel.Y + 7), Color.LightBlue, 1);
        if (_policeSprites is not null)
            batch.Draw(_policeSprites, new Rectangle(panel.X + 23, panel.Y + 31, 48, 64),
                OriginalSpriteLayout.PolicePatrolCar, Color.White);
        DrawCombatForce(batch, pixel, new Rectangle(panel.X + 15, panel.Y + 100, 64, 3),
            ManualRules.MaximumForce);
    }

    private void DrawCombatResultOpponents(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state,
        PlayerId viewer,
        IReadOnlyList<GameEvent> events,
        GameEvent selected,
        bool policeSelected)
    {
        var owners = events.Select(gameEvent => CombatSides(state, viewer, gameEvent))
            .Where(sides => !sides.PoliceEnemy && sides.Enemy is not null)
            .Select(sides => sides.Enemy!.Owner).Distinct().OrderBy(owner => owner.Value).Take(5).ToArray();
        var selectedOwner = policeSelected ? (PlayerId?)null : CombatSides(state, viewer, selected).Enemy?.Owner;
        for (var slot = 0; slot < owners.Length; slot++)
        {
            var player = state.FindPlayer(owners[slot])!;
            var destination = CombatResultsLayout.Opponent(slot);
            if (_uiSprites is not null)
                batch.Draw(_uiSprites, destination,
                    OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
            DrawBorder(batch, pixel, destination, PlayerColors[player.Id.Value],
                player.Id == selectedOwner ? 2 : 1);
        }
    }

    private static void ClearCombatResultPage(SpriteBatch batch, Texture2D pixel)
        => batch.Draw(pixel, new Rectangle(133, 136, 58, 12), Color.Black);
}

public static class CombatResultProjection
{
    public static bool IsFromLastCompletedTurn(int eventTurn, int currentTurn)
    {
        if (eventTurn < 0) throw new ArgumentOutOfRangeException(nameof(eventTurn));
        if (currentTurn < 1) throw new ArgumentOutOfRangeException(nameof(currentTurn));
        return eventTurn == currentTurn - 1;
    }
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

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    /// <summary>
    /// The turn each player's family dispatch last wrote its records for, 0 before its first.
    /// Every driver runs the planning pass and plans from it in one step, so this is neither
    /// saved nor hashed: it only lets <see cref="AiTurnPlanner.Plan"/> refuse records that were
    /// not written this turn (RULE-AI-002).
    /// </summary>
    private readonly int[] _aiPlanningPreparedTurns = new int[MatchLimits.PlayerCount];

    private void RecordAiPlannedAction(GameCommand command)
    {
        var playerId = command.Player;
        var gangId = command.Gang;
        var player = FindPlayer(playerId)!;
        if (!IsPlannedByComputer(playerId)) return;
        for (var gangSlot = 0; gangSlot < player.Gangs.Count; gangSlot++)
        {
            if (player.Gangs[gangSlot].Id != gangId) continue;
            AiPlanning.SetPlannedAction(
                playerId,
                gangSlot,
                command.Action,
                OriginalAiActionTargetEncoding.Encode(this, command));
            return;
        }
        throw new InvalidOperationException("Validated computer gang has no stable player-list slot.");
    }

    public void PrepareAiPlanning(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Command || Coordinator.ActivePlayer != player)
            throw new InvalidOperationException("AI preparation requires that player's active Command phase.");
        if (!IsPlannedByComputer(player))
            throw new ArgumentException("AI preparation requires a computer-controlled player.", nameof(player));
        AiPlanningPreparation.ApplyFamilyAssignments(this, player);
        AiStrategy.ApplySectorCombatAdvantageHostility(this, player);
        AiTurnPlanner.PrepareRecoveredFamilyCommands(this, player);
        AiPlanningPreparation.RefreshHireAnchor(this, player);
    }

    /// <summary>Records that the family dispatch wrote the player's records for this turn.</summary>
    internal void MarkAiPlanningPrepared(PlayerId player) =>
        _aiPlanningPreparedTurns[player.Value] = Coordinator.Turn;

    /// <summary>Whether the player's family dispatch has written its records for this turn.</summary>
    internal bool IsAiPlanningPrepared(PlayerId player) =>
        player.Value is >= 0 and < MatchLimits.PlayerCount
        && _aiPlanningPreparedTurns[player.Value] == Coordinator.Turn;

    public AiTurnPlanner.HirePreparation PrepareAiHiring(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Command || Coordinator.ActivePlayer != player)
            throw new InvalidOperationException("AI hiring preparation requires that player's active Command phase.");
        if (!IsPlannedByComputer(player))
            throw new ArgumentException("AI hiring preparation requires a computer-controlled player.", nameof(player));
        var census = AiPlanningPreparation.TakeHireCensus(this, player);
        var preparation = new AiTurnPlanner.HirePreparation(null);
        if (AiPlanningPreparation.SelectHireRole(this, player, census) is { } selection)
        {
            AiPlanning.SetCurrentHireRole(player, selection.Role);
            // The anchor PrepareAiPlanning refreshed this turn (RULE-AI-013).
            preparation = AiTurnPlanner.PrepareHire(
                this, player, selection, AiPlanning.SectorAnchor(player));
        }
        AiPlanningPreparation.RevertSurplusHunter(this, player, census);
        return preparation;
    }
}

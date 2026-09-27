namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
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
        var weights = RefreshAiSectorRecords(player);
        AiTurnPlanner.PrepareRecoveredFamilyCommands(this, weights);
        AiPlanningPreparation.RefreshHireAnchor(this, player);
    }

    /// <summary>
    /// RULE-AI-003, FND-AI-045: when a match starts or is loaded, the refresh of the planning pass
    /// runs once for every player in slot order, humans included, so each row of sector weights
    /// holds values before its player's first pass and the aliased read of RULE-AI-005 finds a
    /// human's row filled. A local load calls this; a new match runs it as it is built.
    /// </summary>
    internal void RefreshEveryPlayersAiSectorRecords()
    {
        foreach (var player in Players) RefreshAiSectorRecords(player.Id);
    }

    /// <summary>
    /// RULE-AI-003: the sector weights are cached before the hostility step changes attitudes.
    /// </summary>
    private AiTurnPlanner.CachedSectorWeights RefreshAiSectorRecords(PlayerId player)
    {
        var weights = AiTurnPlanner.CachedSectorWeights.Cache(this, player);
        AiStrategy.ApplySectorCombatAdvantageHostility(this, player);
        return weights;
    }

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

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    private void RecordAiPlannedAction(GameCommand command)
    {
        var playerId = command.Player;
        var gangId = command.Gang;
        var player = FindPlayer(playerId)!;
        if (player.Setup.Controller != PlayerController.Computer) return;
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
        if (FindPlayer(player)?.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI preparation requires a computer-controlled player.", nameof(player));
        AiPlanningPreparation.ApplyFamilyAssignments(this, player);
        AiStrategy.ApplySectorCombatAdvantageHostility(this, player);
        AiTurnPlanner.PrepareRecoveredFamilyCommands(this, player);
    }

    public AiTurnPlanner.HirePreparation PrepareAiHiring(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Command || Coordinator.ActivePlayer != player)
            throw new InvalidOperationException("AI hiring preparation requires that player's active Command phase.");
        if (FindPlayer(player)?.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI hiring preparation requires a computer-controlled player.", nameof(player));
        if (AiPlanningPreparation.SelectHireRole(this, player) is not { } selection)
            return new AiTurnPlanner.HirePreparation(null);
        AiPlanning.SetCurrentHireRole(player, selection.Role);
        return AiTurnPlanner.PrepareHire(this, player, selection);
    }
}

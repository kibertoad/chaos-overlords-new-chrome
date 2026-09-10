namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    public void PrepareAiPlanning(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Command || Coordinator.ActivePlayer != player)
            throw new InvalidOperationException("AI preparation requires that player's active Command phase.");
        if (FindPlayer(player)?.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI preparation requires a computer-controlled player.", nameof(player));
        AiPlanningPreparation.ApplyFamilyAssignments(this, player);
        AiStrategy.ApplySectorCombatAdvantageHostility(this, player);
    }

    public AiTurnPlanner.HireChoice? PrepareAiHiring(PlayerId player)
    {
        if (Coordinator.Phase != TurnPhase.Command || Coordinator.ActivePlayer != player)
            throw new InvalidOperationException("AI hiring preparation requires that player's active Command phase.");
        if (FindPlayer(player)?.Setup.Controller != PlayerController.Computer)
            throw new ArgumentException("AI hiring preparation requires a computer-controlled player.", nameof(player));
        if (AiPlanningPreparation.SelectHireRole(this, player) is not { } selection)
            return null;
        AiPlanning.SetCurrentHireRole(player, selection.Role);
        return AiTurnPlanner.ChooseHire(this, player, selection);
    }
}

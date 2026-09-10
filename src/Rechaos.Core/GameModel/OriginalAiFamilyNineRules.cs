namespace Rechaos.Core.GameModel;

/// <summary>
/// Equipment, territorial, and combat branches recovered from original AI
/// family 9 at 0x004605e0.
/// </summary>
internal static class OriginalAiFamilyNineRules
{
    public const int AttackAttempts = 5;

    public static int EquipmentCooldown(int itemCost) =>
        OriginalAiEquipmentRules.EquipmentReplacementCooldown(itemCost);

    public static GangAction SelectTerritorialAction(
        bool ownsCurrentSector,
        int visibleOpponentWeight,
        GangAction previousAction) =>
        ownsCurrentSector
            ? GangAction.Move
            : visibleOpponentWeight == 10 ? GangAction.Attack
            : previousAction == GangAction.Control ? GangAction.Move
            : GangAction.Control;

    public static bool CanAttackSelectedTarget(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int comparisonTargetForce,
        int comparisonTargetCombat,
        int comparisonTargetDefense) =>
        OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            comparisonTargetForce, comparisonTargetCombat,
            comparisonTargetDefense);
}

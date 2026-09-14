namespace Rechaos.Core.GameModel;

public enum GameEventKind : byte
{
    UpkeepResolved,
    CommandQueued,
    CommandReplaced,
    CommandCancelled,
    CommandResolved,
    CommandFailed,
    HireQueued,
    HireResolved,
    HireOfferSnubbed,
    HireOfferRefilled,
    PoliceAttackResolved,
    PlayerEliminated,
    BigManPointsAwarded,
    MatchEnded,
    HireFailed
}

public sealed record CommandResolutionDetails(
    CommandResolutionCode Code,
    IReadOnlyList<int> Rolls,
    int Successes,
    int? PreviousValue = null,
    int? ResultValue = null,
    int CashDelta = 0,
    short? ItemId = null,
    short? ReplacedItemId = null,
    int? AttackValue = null,
    int? DefenseValue = null,
    IReadOnlyList<int>? RetaliationRolls = null,
    int RetaliationSuccesses = 0,
    int Damage = 0,
    int RetaliationDamage = 0,
    int? DetectionRoll = null,
    int? DetectionChance = null,
    int? ChanceRoll = null,
    int? ChanceSides = null,
    short? RetaliationItemId = null,
    IReadOnlyList<short>? ItemIds = null,
    IReadOnlyList<short>? ReplacedItemIds = null);

public sealed record EconomyResolutionDetails(
    int PreviousCash,
    int SectorIncome,
    int SiteIncome,
    int GangUpkeep,
    int ResultCash)
{
    public int NetChange => ResultCash - PreviousCash;
    public bool IsInDebt => ResultCash < 0;
}

public sealed record HireResolutionDetails(
    short GangDefinitionId,
    int SectorId,
    int Cost,
    GangId? Gang = null,
    short? ReplacementOffer = null,
    int? InitialForce = null);

public sealed record HireOfferDetails(short? RemovedOffer, short? AddedOffer);

public sealed record EliminationDetails(PlayerId EliminatedPlayer, int RemainingPlayers);

public sealed record BigManPointDetails(
    int PreviousPoints,
    int ControlledCentralSectors,
    int ResultPoints);

public sealed record MatchOutcomeDetails(
    ScenarioId Scenario,
    MatchEndReason Reason,
    int CompletedTurn,
    IReadOnlyList<PlayerId> Winners,
    IReadOnlyList<MatchStanding> Standings,
    IReadOnlyList<EndgameAwardResult> Awards);

public sealed record PoliceAttackResolutionDetails(
    int SectorId,
    int DetectionChance,
    int DetectionRoll,
    bool Detected,
    int AttackValue,
    int DefenseValue,
    IReadOnlyList<int> Rolls,
    int Successes,
    int Damage,
    int PreviousForce,
    int ResultForce);

/// <summary>An ordered mechanical fact suitable for UI, replay, and parity fixtures.</summary>
public sealed record GameEvent(
    long Sequence,
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    GameEventKind Kind,
    PlayerId Player,
    GangId? Gang,
    GangAction Action,
    CommandTarget Target,
    CommandTarget? SecondaryTarget = null,
    CommandTarget? TertiaryTarget = null,
    CommandTarget? QuaternaryTarget = null,
    CommandResolutionDetails? Resolution = null,
    EconomyResolutionDetails? Economy = null,
    HireResolutionDetails? Hire = null,
    HireOfferDetails? HireOffer = null,
    EliminationDetails? Elimination = null,
    PoliceAttackResolutionDetails? PoliceAttack = null,
    BigManPointDetails? BigManPoints = null,
    MatchOutcomeDetails? MatchOutcome = null);

internal static class GameEventValidator
{
    public static bool IsStructurallyValid(GameEvent value)
    {
        if (value.Turn < 1
            || !Enum.IsDefined(value.Phase)
            || !Enum.IsDefined(value.Kind)
            || !Enum.IsDefined(value.Action)
            || value.ExecutionPhase is { } executionPhase && !Enum.IsDefined(executionPhase)
            || (value.Phase == TurnPhase.Execution) != value.ExecutionPhase.HasValue
            || !IsValidTarget(value.Target)
            || !IsValidTarget(value.SecondaryTarget)
            || !IsValidTarget(value.TertiaryTarget)
            || !IsValidTarget(value.QuaternaryTarget)
            || value.Resolution is { } resolution && !Enum.IsDefined(resolution.Code))
            return false;

        var detailCount = Convert.ToInt32(value.Resolution is not null)
            + Convert.ToInt32(value.Economy is not null)
            + Convert.ToInt32(value.Hire is not null)
            + Convert.ToInt32(value.HireOffer is not null)
            + Convert.ToInt32(value.Elimination is not null)
            + Convert.ToInt32(value.PoliceAttack is not null)
            + Convert.ToInt32(value.BigManPoints is not null)
            + Convert.ToInt32(value.MatchOutcome is not null);
        return value.Kind switch
        {
            GameEventKind.CommandQueued or GameEventKind.CommandReplaced =>
                detailCount == 0 && value.Action != GangAction.None,
            GameEventKind.CommandCancelled =>
                detailCount == 0 && value.Action == GangAction.None,
            GameEventKind.CommandResolved or GameEventKind.CommandFailed =>
                detailCount == 1 && value.Resolution is not null
                    && value.Action != GangAction.None,
            GameEventKind.UpkeepResolved =>
                detailCount == 1 && value.Economy is not null
                    && value.Action == GangAction.None,
            GameEventKind.HireQueued or GameEventKind.HireResolved or GameEventKind.HireFailed =>
                detailCount == 1 && value.Hire is not null
                    && value.Action == GangAction.None,
            GameEventKind.HireOfferSnubbed or GameEventKind.HireOfferRefilled =>
                detailCount == 1 && value.HireOffer is not null
                    && value.Action == GangAction.None,
            GameEventKind.PlayerEliminated =>
                detailCount == 1 && value.Elimination is not null
                    && value.Action == GangAction.None,
            GameEventKind.PoliceAttackResolved =>
                detailCount == 1 && value.PoliceAttack is not null
                    && value.Action == GangAction.None,
            GameEventKind.BigManPointsAwarded =>
                detailCount == 1 && value.BigManPoints is not null
                    && value.Action == GangAction.None,
            // A match everybody lost carries no winner; see MatchOutcomeEvaluator.
            GameEventKind.MatchEnded =>
                detailCount == 1 && value.MatchOutcome is not null
                    && value.Action == GangAction.None,
            _ => false
        };
    }

    private static bool IsValidTarget(CommandTarget? value) =>
        value is null || IsValidTarget(value.Value);

    private static bool IsValidTarget(CommandTarget value) => value.Kind switch
    {
        CommandTargetKind.None => value.Id == -1,
        CommandTargetKind.Gang => value.Id >= 0,
        CommandTargetKind.Sector => value.Id is >= 0 and < MatchLimits.SectorCount,
        CommandTargetKind.Site => value.Id is >= 0 and < MatchLimits.SiteCount,
        CommandTargetKind.Item => value.Id is >= 0 and < MatchLimits.ItemSlots,
        _ => false
    };
}

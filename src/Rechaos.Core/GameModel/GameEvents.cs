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
    MatchEnded
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
    int? DetectionChance = null);

public sealed record EconomyResolutionDetails(
    int PreviousCash,
    int SectorIncome,
    int SiteIncome,
    int GangUpkeep,
    int ResultCash)
{
    public int NetChange => ResultCash - PreviousCash;
    public bool WasFlooredAtZero => PreviousCash + SectorIncome + SiteIncome - GangUpkeep < 0;
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
    CommandResolutionDetails? Resolution = null,
    EconomyResolutionDetails? Economy = null,
    HireResolutionDetails? Hire = null,
    HireOfferDetails? HireOffer = null,
    EliminationDetails? Elimination = null,
    PoliceAttackResolutionDetails? PoliceAttack = null,
    BigManPointDetails? BigManPoints = null,
    MatchOutcomeDetails? MatchOutcome = null);

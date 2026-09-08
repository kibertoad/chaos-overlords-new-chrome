namespace Rechaos.Core.GameModel;

public enum GameEventKind : byte
{
    UpkeepResolved,
    CommandQueued,
    CommandReplaced,
    CommandCancelled,
    CommandResolved,
    CommandFailed
}

public sealed record CommandResolutionDetails(
    CommandResolutionCode Code,
    IReadOnlyList<int> Rolls,
    int Successes,
    int? PreviousValue = null,
    int? ResultValue = null,
    int CashDelta = 0);

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
    EconomyResolutionDetails? Economy = null);

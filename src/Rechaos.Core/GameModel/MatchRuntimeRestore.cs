namespace Rechaos.Core.GameModel;

internal sealed record MatchRuntimeRestore(
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    PlayerId? ActivePlayer,
    uint RandomState,
    long RandomConsumptionCount,
    IReadOnlyList<QueuedCommand> Commands,
    long NextCommandSequence,
    IReadOnlyList<GameEvent> Events,
    long NextEventSequence,
    IReadOnlyDictionary<PlayerId, IReadOnlyList<GameNotification>> Notifications,
    IReadOnlyDictionary<PlayerId, long> NextNotificationSequences,
    IReadOnlyDictionary<PlayerId, ComlinkInboxRestore> ComlinkInboxes,
    IReadOnlyList<PhaseBoundaryHash> PhaseHashes,
    MatchOutcome? Outcome,
    AiStrategicState AiStrategy,
    AiPlanningState AiPlanning);

internal sealed record ComlinkInboxRestore(
    IReadOnlyList<ComlinkMessage> Messages,
    long NextSequence,
    long ReadThroughSequence);

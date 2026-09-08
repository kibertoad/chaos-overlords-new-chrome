namespace Rechaos.Core.GameModel;

public enum GameEventKind : byte
{
    CommandQueued,
    CommandReplaced,
    CommandCancelled,
    CommandResolved
}

/// <summary>An ordered mechanical fact suitable for UI, replay, and parity fixtures.</summary>
public sealed record GameEvent(
    long Sequence,
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    GameEventKind Kind,
    PlayerId Player,
    GangId Gang,
    GangAction Action,
    CommandTarget Target,
    CommandTarget? SecondaryTarget = null);

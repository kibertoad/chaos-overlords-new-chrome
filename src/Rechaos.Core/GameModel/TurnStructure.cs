namespace Rechaos.Core.GameModel;

/// <summary>The five top-level phases documented by the original manual.</summary>
public enum TurnPhase
{
    Upkeep,
    Command,
    Execution,
    Hire,
    PlayerElimination
}

/// <summary>The fixed execution ordering shared by all players.</summary>
public enum ExecutionPhase
{
    Instant,
    Combat,
    Transaction,
    Chaos,
    Movement,
    Control
}

public enum GangAction : byte
{
    None = 0,
    Attack = 1,
    Bribe = 2,
    Chaos = 3,
    Control = 4,
    Equip = 5,
    Give = 6,
    Heal = 7,
    Hide = 8,
    Influence = 9,
    Move = 10,
    Research = 11,
    Sell = 12,
    Snitch = 13,
    Terminate = 14
}

public static class TurnStructure
{
    public static readonly IReadOnlyList<TurnPhase> TurnOrder =
        [TurnPhase.Upkeep, TurnPhase.Command, TurnPhase.Execution, TurnPhase.Hire, TurnPhase.PlayerElimination];

    public static readonly IReadOnlyList<ExecutionPhase> ExecutionOrder =
        [ExecutionPhase.Instant, ExecutionPhase.Combat, ExecutionPhase.Transaction,
         ExecutionPhase.Chaos, ExecutionPhase.Movement, ExecutionPhase.Control];

    public static ExecutionPhase PhaseFor(GangAction action) => action switch
    {
        GangAction.Bribe or GangAction.Heal or GangAction.Hide or GangAction.Influence
            or GangAction.Research or GangAction.Snitch => ExecutionPhase.Instant,
        GangAction.Attack => ExecutionPhase.Combat,
        GangAction.Equip or GangAction.Give or GangAction.Sell => ExecutionPhase.Transaction,
        GangAction.Chaos => ExecutionPhase.Chaos,
        GangAction.Move or GangAction.Terminate => ExecutionPhase.Movement,
        GangAction.Control => ExecutionPhase.Control,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "The action does not execute.")
    };

    public static int ExecutionIndex(ExecutionPhase phase)
    {
        for (var index = 0; index < ExecutionOrder.Count; index++)
            if (ExecutionOrder[index] == phase) return index;
        throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown execution phase.");
    }
}

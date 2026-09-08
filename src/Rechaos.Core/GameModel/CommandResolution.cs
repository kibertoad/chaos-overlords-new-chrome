namespace Rechaos.Core.GameModel;

public enum CommandResolutionCode : byte
{
    Resolved,
    InsufficientCash,
    UnsupportedAction
}

public sealed record CommandResolutionResult(
    GameCommand Command,
    CommandResolutionCode Code,
    GameEvent? Event)
{
    public bool Succeeded => Code == CommandResolutionCode.Resolved;
}

/// <summary>
/// Deterministic action dispatch. Only actions backed by recorded evidence are
/// enabled; unsupported actions are rejected before a subphase mutates state.
/// </summary>
public static class CommandResolver
{
    public static bool IsSupported(GangAction action) => action is GangAction.Bribe or GangAction.Snitch;

    public static CommandResolutionResult Resolve(MatchState state, QueuedCommand queued)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(queued);
        if (state.Coordinator.Phase != TurnPhase.Execution
            || state.Coordinator.ExecutionPhase != queued.ExecutionPhase)
            throw new InvalidOperationException("A command can only resolve during its execution subphase.");

        return queued.Command.Action switch
        {
            GangAction.Bribe => ResolveBribe(state, queued.Command),
            GangAction.Snitch => ResolveSnitch(state, queued.Command),
            _ => new CommandResolutionResult(queued.Command, CommandResolutionCode.UnsupportedAction, null)
        };
    }

    private static CommandResolutionResult ResolveBribe(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var cost = CommandRules.ByAction[GangAction.Bribe].CashCost;
        if (player.Cash < cost)
            return Complete(state, command, CommandResolutionCode.InsufficientCash, GameEventKind.CommandFailed);

        var gang = state.FindGang(command.Gang)!;
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        state.Sectors[gang.SectorId].Tolerance = ManualRules.ApplyBribe(state.Sectors[gang.SectorId].Tolerance);
        return Complete(state, command, CommandResolutionCode.Resolved, GameEventKind.CommandResolved);
    }

    private static CommandResolutionResult ResolveSnitch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        state.Sectors[gang.SectorId].Tolerance = ManualRules.ApplySnitch(state.Sectors[gang.SectorId].Tolerance);
        return Complete(state, command, CommandResolutionCode.Resolved, GameEventKind.CommandResolved);
    }

    private static CommandResolutionResult Complete(
        MatchState state,
        GameCommand command,
        CommandResolutionCode code,
        GameEventKind eventKind)
    {
        var gameEvent = state.AppendResolutionEvent(eventKind, command, code);
        state.QueueNotification(
            command.Player,
            GameNotificationKind.CommandResult,
            command.Gang,
            state.FindGang(command.Gang)!.SectorId,
            gameEvent.Sequence);
        return new CommandResolutionResult(command, code, gameEvent);
    }
}

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
    public static bool IsSupported(GangAction action) =>
        action is GangAction.Bribe or GangAction.Heal or GangAction.Snitch;

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
            GangAction.Heal => ResolveHeal(state, queued.Command),
            GangAction.Snitch => ResolveSnitch(state, queued.Command),
            _ => new CommandResolutionResult(queued.Command, CommandResolutionCode.UnsupportedAction, null)
        };
    }

    private static CommandResolutionResult ResolveBribe(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var cost = CommandRules.ByAction[GangAction.Bribe].CashCost;
        if (player.Cash < cost)
        {
            var tolerance = state.Sectors[state.FindGang(command.Gang)!.SectorId].Tolerance;
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(CommandResolutionCode.InsufficientCash, [], 0, tolerance, tolerance));
        }

        var gang = state.FindGang(command.Gang)!;
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        var before = state.Sectors[gang.SectorId].Tolerance;
        var after = ManualRules.ApplyBribe(before);
        state.Sectors[gang.SectorId].Tolerance = after;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, after, -cost));
    }

    private static CommandResolutionResult ResolveHeal(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var rolls = DiceRoller.RollD6(state.Random, ManualRules.HealDiceCount(statistics.Heal));
        var successes = ManualRules.CountSuccesses(rolls);
        var before = gang.Force;
        gang.Force = ManualRules.RestoreForce(gang.Force, successes);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, rolls, successes, before, gang.Force));
    }

    private static CommandResolutionResult ResolveSnitch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = state.Sectors[gang.SectorId].Tolerance;
        var after = ManualRules.ApplySnitch(before);
        state.Sectors[gang.SectorId].Tolerance = after;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, after));
    }

    private static CommandResolutionResult Complete(
        MatchState state,
        GameCommand command,
        GameEventKind eventKind,
        CommandResolutionDetails resolution)
    {
        var gameEvent = state.AppendResolutionEvent(eventKind, command, resolution);
        state.QueueNotification(
            command.Player,
            GameNotificationKind.CommandResult,
            command.Gang,
            state.FindGang(command.Gang)!.SectorId,
            gameEvent.Sequence);
        return new CommandResolutionResult(command, resolution.Code, gameEvent);
    }
}

namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    /// <summary>
    /// Applies the original turn-start terminal checks for retained commands before any
    /// Crackdown duration or sector-benefit update can change the facts they inspect.
    /// </summary>
    private void NormalizeRecurringCommands()
    {
        foreach (var queued in Commands.ExecutionPlan().Where(value => value.Command.Repeat))
        {
            var command = queued.Command;
            var gang = FindGang(command.Gang);
            var shouldStop = gang is null || !gang.IsActive || command.Action switch
            {
                GangAction.Control => Sectors[gang.SectorId].Owner == command.Player
                    || Sectors[gang.SectorId].CrackdownActive,
                GangAction.Heal => gang.Force >= ManualRules.MaximumForce,
                GangAction.Influence => FindSite(command.Target.Id) is { } site
                    && (site.Resistance == 0
                        || Sectors[command.Target.Id / MatchLimits.SitesPerSector].Owner != command.Player),
                GangAction.Research => FindPlayer(command.Player)!.ResearchedItems
                    .Contains(checked((short)command.Target.Id)),
                _ => false
            };
            if (!shouldStop) continue;

            Commands.Cancel(command.Gang);
            if (gang is not null) gang.QueuedCommand = null;
        }
    }

    private bool ShouldStopResolvedCommand(CommandResolutionResult result)
    {
        if (!result.Succeeded) return false;
        var command = result.Command;
        var gang = FindGang(command.Gang);
        // Site ownership is deliberately activated at the next planning boundary. Resistance
        // reaching zero is nevertheless the point at which Influence is finished, so release the
        // gang immediately instead of making a recurring command spend another turn discovering it.
        if (command.Action == GangAction.Influence)
            return result.Event?.Resolution?.ResultValue == 0;
        if (!command.Repeat) return false;
        return command.Action switch
        {
            GangAction.Attack => FindGang(new GangId(command.Target.Id)) is not { IsActive: true },
            GangAction.Control => gang is not null && Sectors[gang.SectorId].Owner == command.Player,
            GangAction.Equip or GangAction.Give or GangAction.Sell or GangAction.Move
                or GangAction.Terminate => true,
            GangAction.Heal => gang is null || gang.Force >= ManualRules.MaximumForce,
            GangAction.Research => FindPlayer(command.Player)!.ResearchedItems.Contains((short)command.Target.Id),
            GangAction.Snitch => gang is not null && Sectors[gang.SectorId].Tolerance <= 0,
            _ => false
        };
    }
}

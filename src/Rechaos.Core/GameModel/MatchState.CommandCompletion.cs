namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
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

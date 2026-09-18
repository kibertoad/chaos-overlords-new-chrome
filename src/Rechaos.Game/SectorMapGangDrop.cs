using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Interprets a friendly gang dragged onto the detailed sector view's 3-by-3 minimap.
/// A neighboring tile queues a one-off Move, while the tile the gang already occupies
/// queues a recurring Control so the gang keeps working the sector until it falls.
/// </summary>
public static class SectorMapGangDrop
{
    public static GameCommand? Resolve(
        IReadOnlyList<GameCommand> legalCommands,
        int gangSectorId,
        int dropSectorId)
    {
        ArgumentNullException.ThrowIfNull(legalCommands);
        if (dropSectorId == gangSectorId)
            return legalCommands.FirstOrDefault(command => command.Action == GangAction.Control)
                is { } control
                ? control with { Repeat = true }
                : null;
        return legalCommands.FirstOrDefault(command => command.Action == GangAction.Move
            && command.Target == CommandTarget.Sector(dropSectorId)) is { } move
            ? move with { Repeat = false }
            : null;
    }

    /// <summary>Sectors the drag may legally drop on, for destination highlighting.</summary>
    public static IReadOnlySet<int> Destinations(
        IReadOnlyList<GameCommand> legalCommands,
        int gangSectorId)
    {
        ArgumentNullException.ThrowIfNull(legalCommands);
        var destinations = legalCommands
            .Where(command => command.Action == GangAction.Move)
            .Select(command => command.Target.Id)
            .ToHashSet();
        if (legalCommands.Any(command => command.Action == GangAction.Control))
            destinations.Add(gangSectorId);
        return destinations;
    }

    /// <summary>Status-console text explaining a drop the rules reject.</summary>
    public static string Rejection(MatchState state, MatchGangState gang, int dropSectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        if (dropSectorId != gang.SectorId) return "MOVE REQUIRES NEIGHBOR SECTOR";
        var control = new GameCommand(gang.Owner, gang.Id, GangAction.Control, CommandTarget.None);
        return CommandValidator.Validate(state, control).Code switch
        {
            CommandValidationCode.SectorAlreadyControlled => "SECTOR ALREADY CONTROLLED",
            CommandValidationCode.SectorInCrackdown => "POLICE BLOCK CONTROL ATTEMPT",
            _ => "CONTROL IS NOT AVAILABLE"
        };
    }
}

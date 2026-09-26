using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Interprets a friendly gang dragged onto the detailed sector view's 3-by-3 minimap.
/// A neighboring tile means a one-off Move, while the tile the gang already occupies
/// means a recurring Control so the gang keeps working the sector until it falls.
/// </summary>
public static class SectorMapGangDrop
{
    /// <summary>
    /// What the drop asks for. It reads the map alone: whether the rules allow it is the
    /// validator's answer, and the same drop may be legal for some of a selection and not others.
    /// </summary>
    public static BulkCommandIntent Intent(int gangSectorId, int dropSectorId) =>
        dropSectorId == gangSectorId
            ? new BulkCommandIntent(GangAction.Control, CommandTarget.None, Repeat: true)
            : new BulkCommandIntent(
                GangAction.Move, CommandTarget.Sector(dropSectorId), Repeat: false);

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
        if (dropSectorId != gang.SectorId)
        {
            var move = new GameCommand(
                gang.Owner, gang.Id, GangAction.Move, CommandTarget.Sector(dropSectorId));
            return CommandValidator.Validate(state, move).Code switch
            {
                CommandValidationCode.DestinationAtCapacity => "SECTOR IS FULL",
                CommandValidationCode.DestinationNotAdjacent => "MOVE REQUIRES NEIGHBOR SECTOR",
                _ => "MOVE IS NOT AVAILABLE"
            };
        }
        var control = new GameCommand(gang.Owner, gang.Id, GangAction.Control, CommandTarget.None);
        return CommandValidator.Validate(state, control).Code switch
        {
            CommandValidationCode.SectorAlreadyControlled => "SECTOR ALREADY CONTROLLED",
            CommandValidationCode.SectorInCrackdown => "POLICE BLOCK CONTROL ATTEMPT",
            _ => "CONTROL IS NOT AVAILABLE"
        };
    }
}

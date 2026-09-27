using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private readonly GangMultiSelection _gangSelection = new();

    /// <summary>Whether ctrl is down, which turns a click on a gang card into a pick.</summary>
    private bool _multiSelectModifier;

    /// <summary>Whether the command overlay is ordering the whole ctrl-picked selection.</summary>
    private bool _bulkCommand;

    /// <summary>
    /// Whether the overlay orders the sector's gangs, for the group order strip, which lists the
    /// original's group menus.
    /// </summary>
    private bool _groupCommand;

    /// <summary>
    /// The gangs a bulk command overlay orders: the ctrl-picked selection, or for a group order
    /// every gang of the sector, which leaves the player's own pick as it was.
    /// </summary>
    private IReadOnlyList<GangId> _bulkCommandGangs = [];

    /// <summary>
    /// Whether the ctrl-picked selection survives the screen now showing, which
    /// <see cref="GangSelectionScreens"/> decides from the panels standing open.
    /// </summary>
    private bool KeepsGangSelection(ClientScreen screen) =>
        GangSelectionScreens.Keeps(screen, new PanelReturnScreens
        {
            Commands = _commandReturnScreen,
            GangDetails = _gangDetailsReturnScreen,
            SiteDetails = _siteDetailsReturnScreen,
            SectorGangs = _sectorGangReturnScreen
        });

    /// <summary>Adds the gang to the ctrl-picked selection, or takes it back out.</summary>
    private void ToggleGangSelection(MatchGangState gang)
    {
        _gangSelection.Toggle(gang.Id, gang.SectorId);
        AcceptInput();
        _message = _gangSelection.Count == 0
            ? string.Empty
            : GangMultiSelection.Status(_gangSelection.Count);
    }

    /// <summary>
    /// Whether an order given on this card is meant for the whole selection. One picked gang is
    /// still one gang, and keeps the full command list rather than the bulk allowlist.
    /// </summary>
    private bool IsSelectedForBulkCommand(MatchGangState gang) =>
        _gangSelection.IsBulk && _gangSelection.Contains(gang.Id);

    /// <summary>
    /// Puts one order to every picked gang, keeping the gangs the rules allow and leaving the
    /// rest as they were, and reports how much of the selection took it.
    /// </summary>
    /// <returns>Whether any gang took the order.</returns>
    private bool ApplyBulkCommand(PlayerId player, BulkCommandIntent intent, string rejection) =>
        ApplyBulkCommand(player, _gangSelection.Gangs, group: false, intent, rejection);

    /// <summary>
    /// Puts one order to <paramref name="gangs"/>. The ctrl-picked selection is used up by the
    /// order; a group order was never the pick, so it leaves the pick alone.
    /// </summary>
    /// <param name="group">Whether the gangs are a group order's, checked against the group
    /// menus' allowlist rather than the ctrl-pick one.</param>
    private bool ApplyBulkCommand(
        PlayerId player, IReadOnlyList<GangId> gangs, bool group, BulkCommandIntent intent, string rejection)
    {
        if (_state is null || _actions is null) return false;
        var selected = gangs.Count;
        var plan = BulkGangCommands.Plan(_state, player, gangs, intent, group);
        var ordered = 0;
        foreach (var command in plan.Commands)
            if (_actions.Submit(command).Accepted) ordered++;
        if (ordered == 0)
        {
            RejectInput(rejection);
            return false;
        }
        AcceptInput();
        _message = BulkGangCommands.Message(intent.Action, ordered, selected);
        if (!group) _gangSelection.Clear();
        return true;
    }
}

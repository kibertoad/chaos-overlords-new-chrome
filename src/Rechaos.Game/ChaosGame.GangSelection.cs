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
    private bool ApplyBulkCommand(PlayerId player, BulkCommandIntent intent, string rejection)
    {
        if (_state is null || _actions is null) return false;
        var selected = _gangSelection.Count;
        var plan = BulkGangCommands.Plan(_state, player, _gangSelection.Gangs, intent);
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
        _gangSelection.Clear();
        return true;
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The Cancel and confirm faces, keys and panel-wide pointer rules the Equip, Research, Give,
/// Sell and Move panels share (SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001).
/// </summary>
public sealed partial class ChaosGame
{
    private CommandPanelFaceState _commandPanelFace;
    private CommandPanelButton? _pressedCommandPanelButton;
    private readonly IndexedDoubleClickTracker _commandPanelClicks = new();

    /// <summary>Whether one of the panels with the shared faces is the one taking input.</summary>
    private bool CommandPanelOpen => _screens.Current switch
    {
        ClientScreen.Give or ClientScreen.Sell => true,
        ClientScreen.Commands => _choosingCommandTarget
            && (IsEquipmentCommandPicker() || IsMovementCommandPicker()),
        _ => false
    };

    private bool CanConfirmCommandPanel() => _screens.Current switch
    {
        ClientScreen.Give => GiveReady(),
        ClientScreen.Sell => _sellSelections.Any(selected => selected),
        _ => CanConfirmCommandTarget()
    };

    private bool CanConfirmCommandTarget()
    {
        if (_commandTargetCursor < 0 || _commandTargetCursor >= _commandTargetOptions.Count) return false;
        return !IsEquipmentCommandPicker() || (_state is not null
            && EquipmentCommandLayout.CanConfirm(_commandTargetCursor, EquipmentCommandIndices(_state)));
    }

    /// <summary>
    /// A press on either face: a confirm the panel cannot take is refused with slot 4 at once;
    /// otherwise the held-button helper plays slot 3 and the face acts on a release inside
    /// (FND-EQUIP-010, FND-GIVE-001, FND-SELL-001, FND-MOVE-004).
    /// </summary>
    private void BeginCommandPanelButton(CommandPanelButton button)
    {
        if (button == CommandPanelButton.Confirm && !CanConfirmCommandPanel())
        {
            RejectInput("NOTHING TO CONFIRM");
            return;
        }
        _pressedCommandPanelButton = button;
        AcceptInput();
    }

    private void CompleteCommandPanelButton(Point point)
    {
        var button = _pressedCommandPanelButton;
        CancelCommandPanelButton();
        if (button is not { } pressed || !CommandPanelOpen
            || !CommandPanelFaces.Hit(pressed).Contains(point)) return;
        if (pressed == CommandPanelButton.Cancel) CancelCommandPanel();
        else ConfirmCommandPanel(pointerButton: true);
    }

    private void CancelCommandPanelButton() => _pressedCommandPanelButton = null;

    private void CancelCommandPanel()
    {
        switch (_screens.Current)
        {
            case ClientScreen.Give:
                CloseGiveEquipment();
                break;
            case ClientScreen.Sell:
                CloseSellEquipment();
                break;
            default:
                BackFromCommands();
                break;
        }
    }

    private void ConfirmCommandPanel(bool pointerButton)
    {
        switch (_screens.Current)
        {
            case ClientScreen.Give:
                QueueSelectedGive(pointerButton);
                break;
            case ClientScreen.Sell:
                QueueSelectedSale(pointerButton);
                break;
            default:
                ActivateCommandSelection(pointerButton);
                break;
        }
    }

    /// <summary>Escape presses Cancel on these panels instead of opening the game menu.</summary>
    private void CancelCommandPanelWithEscape()
    {
        CancelCommandPanelButton();
        AcceptAndInvoke(CancelCommandPanel);
    }

    /// <summary>A press outside the panel is refused with slot 4 and leaves it open.</summary>
    private void RejectOutsideCommandPanel() => RejectInput("CLICK INSIDE THE PANEL");

    private void DrawCommandPanelFaces(SpriteBatch batch)
    {
        if (_uiSprites is null) return;
        if (CommandPanelFaces.Source(_commandPanelFace) is { } source)
            batch.Draw(_uiSprites, CommandPanelFaces.Face(CommandPanelButton.Confirm), source, Color.White);
        if (_pressedCommandPanelButton is { } pressed && _hoverPoint is { } hover
            && CommandPanelFaces.Hit(pressed).Contains(hover))
            batch.Draw(_uiSprites, CommandPanelFaces.Face(pressed),
                CommandPanelFaces.HeldSource(pressed), Color.White);
    }

    /// <summary>
    /// SCR-EQUIP-001: the panel opens on category 0, or, when the gang already has an Equip
    /// order, on the category of its item with that row chosen when it is listed and the
    /// confirm face drawn enabled either way (FND-EQUIP-009, FND-EQUIP-010).
    /// </summary>
    private void OpenEquipmentPurchasePanel()
    {
        _equipmentCategory = 0;
        _commandTargetCursor = -1;
        _commandPanelFace = CommandPanelFaceState.NotDrawn;
        _commandPanelClicks.Cancel();
        if (_state?.FindGang(_commandTargetOptions[0].Gang)?.QueuedCommand?.Command is not
            { Action: GangAction.Equip, Target.Kind: CommandTargetKind.Item } queued) return;
        var item = _state.Definitions.Items[queued.Target.Id];
        _equipmentCategory = EquipmentCommandLayout.CategoryForItemType(item.Type);
        _commandPanelFace = CommandPanelFaces.OnOpening(true);
        _commandTargetCursor = EquipmentCommandIndices(_state)
            .FirstOrDefault(index => _commandTargetOptions[index].Target.Id == queued.Target.Id, -1);
    }

    private void HandleEquipmentCommandClick(Point point)
    {
        if (CommandPanelFaces.ButtonAt(point) is { } button)
        {
            BeginCommandPanelButton(button);
            return;
        }
        if (!EquipmentCommandLayout.Panel.Contains(point))
        {
            RejectOutsideCommandPanel();
            return;
        }
        for (var category = 0; category < EquipmentCommandLayout.CategoryCount; category++)
        {
            if (!EquipmentCommandLayout.CategoryHit(category).Contains(point)) continue;
            SelectEquipmentCategory(category);
            return;
        }
        if (_state is null || _state.FindGang(_commandTargetOptions[0].Gang) is not { } actor) return;
        if (EquipmentCommandLayout.Portrait.Contains(point))
        {
            // One target only, so every portrait click registers the same index.
            if (_equipmentPortraitClicks.Register(0, _inputTime))
                OpenGangDetails(actor, ClientScreen.Commands);
            return;
        }
        var purchase = _commandTargetOptions[0].Action == GangAction.Equip;
        if (purchase)
        {
            var carried = EquippedItems(actor);
            for (var slot = 0; slot < EquipmentCommandLayout.EquippedItemCount; slot++)
            {
                if (!EquipmentCommandLayout.EquippedItem(slot).Contains(point)) continue;
                // SCR-EQUIP-001: a double-click on a carried item's icon opens SCR-UI-006.
                if (_commandPanelClicks.Register(slot, _inputTime) && carried[slot] is { } itemId)
                    OpenItemDetails(itemId);
                return;
            }
        }
        var indices = EquipmentCommandIndices(_state);
        var position = indices.IndexOf(_commandTargetCursor);
        var itemFirst = EquipmentCommandLayout.FirstVisibleItem(indices.Count, Math.Max(0, position));
        var itemVisible = Math.Min(EquipmentCommandLayout.VisibleItemCount, indices.Count - itemFirst);
        var itemRow = EquipmentCommandLayout.ItemRowAt(point);
        if (!purchase)
        {
            // SCR-RESEARCH-001: a press selects from panel y 26 and a double-click opens from
            // panel y 19, each over its own rectangle.
            var detailRow = EquipmentCommandLayout.ResearchItemDetailRowAt(point);
            if (detailRow >= 0 && detailRow < itemVisible)
            {
                var detailItem = _commandTargetOptions[indices[itemFirst + detailRow]].Target.Id;
                if (_equipmentItemClicks.Register(detailItem, _inputTime))
                    OpenItemDetails((short)detailItem);
            }
        }
        if (itemRow < 0) return;
        if (itemRow >= itemVisible)
        {
            // An empty row clears the choice and draws the face disabled (FND-EQUIP-010).
            _commandTargetCursor = -1;
            _commandPanelFace = CommandPanelFaces.AfterChange(false);
            return;
        }
        _commandTargetCursor = indices[itemFirst + itemRow];
        _commandPanelFace = CommandPanelFaces.AfterChange(true);
        if (!purchase) return;
        var chosenItem = _commandTargetOptions[_commandTargetCursor].Target.Id;
        if (_equipmentItemClicks.Register(chosenItem, _inputTime)) OpenItemDetails((short)chosenItem);
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The Cancel and confirm faces, keys and panel-wide pointer rules the Equip, Research, Give,
/// Sell, Move and Influence panels share (SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001).
/// </summary>
public sealed partial class ChaosGame
{
    private CommandPanelFaceState _commandPanelFace;
    private CommandPanelButton? _pressedCommandPanelButton;
    private readonly IndexedDoubleClickTracker _commandPanelClicks = new();

    /// <summary>
    /// Whether one of the panels with the shared faces is the one taking input: the Equip,
    /// Research, Influence, Move, Give and Sell panels, whose keys all press the faces through
    /// fn_00418CCC (FND-EQUIP-010, FND-INFLUENCE-003, FND-MOVE-007, FND-RESEARCH-005,
    /// FND-GIVE-001, FND-SELL-001).
    /// </summary>
    private bool CommandPanelOpen => _screens.Current switch
    {
        ClientScreen.Give or ClientScreen.Sell => true,
        ClientScreen.Commands => _choosingCommandTarget
            && (IsEquipmentCommandPicker() || IsInfluenceCommandPicker() || IsMovementCommandPicker()),
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

    /// <summary>
    /// Escape presses Cancel on these panels instead of opening the game menu. The Equip,
    /// Influence, Move, Research, Give and Sell handlers draw the Cancel face pressed for one
    /// tick before they close (fn_00418CCC; FND-EQUIP-010, FND-INFLUENCE-003, FND-MOVE-007,
    /// FND-RESEARCH-005, FND-GIVE-001, FND-SELL-001, RULE-TIMER-004).
    /// </summary>
    private void CancelCommandPanelWithEscape()
    {
        CancelCommandPanelButton();
        PressKeyFace(PressedKeyFace.Cancel, CommandPanelFaces.Face(CommandPanelButton.Cancel).Location,
            CancelCommandPanel);
    }

    /// <summary>
    /// Enter or Execute on a panel with the shared faces: a confirm the panel cannot take is
    /// refused with slot 4 at once; otherwise the Equip, Influence, Move, Research, Give and Sell
    /// handlers press the confirm face for one tick before the order is written (fn_00418CCC;
    /// FND-EQUIP-010, FND-INFLUENCE-003, FND-MOVE-007, FND-RESEARCH-005, FND-GIVE-001,
    /// FND-SELL-001, RULE-TIMER-004).
    /// </summary>
    private void ConfirmCommandPanelByKey()
    {
        if (!CommandPanelOpen || !CanConfirmCommandPanel())
        {
            ConfirmCommandPanel(pointerButton: false);
            return;
        }
        PressKeyFace(PressedKeyFace.Confirm, CommandPanelFaces.Face(CommandPanelButton.Confirm).Location,
            () => ConfirmCommandPanel(pointerButton: true));
    }

    /// <summary>The Commands screen's Enter: the panels above press their face first.</summary>
    private void ConfirmCommandsByKey()
    {
        if (CommandPanelOpen) ConfirmCommandPanelByKey();
        else ActivateCommandSelection();
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

    /// <summary>
    /// SCR-RESEARCH-001, EXP-UI-009: the panel opens on category 0 with no row selected, unless the
    /// gang's order is already Research, when it opens on that item's category with its row
    /// selected.
    /// </summary>
    private void OpenResearchPanel()
    {
        _equipmentCategory = 0;
        _commandTargetCursor = -1;
        if (_state?.FindGang(_commandTargetOptions[0].Gang)?.QueuedCommand?.Command is not
            { Action: GangAction.Research, Target.Kind: CommandTargetKind.Item } queued) return;
        _equipmentCategory = EquipmentCommandLayout.CategoryForItemType(_state.Definitions.Items[queued.Target.Id].Type);
        _commandTargetCursor = EquipmentCommandIndices(_state)
            .FirstOrDefault(index => _commandTargetOptions[index].Target.Id == queued.Target.Id, -1);
    }

    // FND-INFLUENCE-002, FND-INFLUENCE-005: the mode 0 copy of a completed site keeps the picture
    // where pattern 147 takes the source and the black underneath elsewhere, by screen phase.
    private readonly Dictionary<(int X, int Y), Texture2D> _influenceCompletedMasks = [];

    /// <summary>
    /// SCR-INFLUENCE-001, FND-INFLUENCE-002, FND-INFLUENCE-005, EXP-UI-010: a completed site is its
    /// picture through pattern 147 on black under the completed frame; another site is its picture
    /// under the site frame, or under the chosen frame once chosen.
    /// </summary>
    private void DrawInfluenceSite(
        SpriteBatch batch, Texture2D pixel, MatchSiteState site, Rectangle destination, bool completed, bool chosen)
    {
        if (completed) batch.Draw(pixel, destination, Color.Black);
        if (_sitePortraits is not null)
            batch.Draw(_sitePortraits, destination, OriginalSpriteLayout.SitePortrait(site.DefinitionId), Color.White);
        if (completed) batch.Draw(InfluenceCompletedMask(destination), destination, Color.White);
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, destination,
                completed ? InfluenceCommandLayout.CompletedSiteFrameSource
                : chosen ? InfluenceCommandLayout.ChosenSiteFrameSource
                : InfluenceCommandLayout.SiteFrameSource, Color.White);
    }

    private Texture2D InfluenceCompletedMask(Rectangle destination)
    {
        var phase = (destination.X & 7, destination.Y & 1);
        if (_influenceCompletedMasks.TryGetValue(phase, out var mask)) return mask;
        var pixels = new Color[destination.Width * destination.Height];
        for (var y = 0; y < destination.Height; y++)
        for (var x = 0; x < destination.Width; x++)
            pixels[y * destination.Width + x] = OriginalPatternMask.PreservesDestination(
                OriginalPatternMask.Dense, destination.X + x, destination.Y + y) ? Color.Black : Color.Transparent;
        mask = new Texture2D(GraphicsDevice, destination.Width, destination.Height);
        mask.SetData(pixels);
        _influenceCompletedMasks[phase] = mask;
        return mask;
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

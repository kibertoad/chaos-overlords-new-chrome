using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class EquipmentGiveLayout
{
    public const int RecipientCount = 5;

    /// <summary>
    /// SCR-GIVE-001, FND-GIVE-003: the list builder sets the pattern from the grey 48,000, which
    /// selects bitmap 146, and draws black through it over an ineligible card, starting the
    /// pattern at the card's corner. It fills no background: the list shows the panel image
    /// wherever no card is drawn.
    /// </summary>
    public static int IneligibleCardPattern => OriginalPatternMask.ForGrey(48000);
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    /// <summary>
    /// Native Give handler 0x00445a4f's half-open item-selection target (FND-EQUIP-003).
    /// </summary>
    public static Rectangle ItemHit(int slot)
    {
        ValidateItemSlot(slot);
        return SharedPanelLayout.At(103, 15 + slot * 64, 52, 52);
    }

    /// <summary>The carried item's animated 48-by-48 picture (SCR-GIVE-001, FND-GIVE-001).</summary>
    public static Rectangle ItemPicture(int slot)
    {
        ValidateItemSlot(slot);
        return new Rectangle(209, 141 + slot * 64, 48, 48);
    }

    /// <summary>The selection frame, one pixel outside the item cell (SCR-GIVE-001, FND-GIVE-002).</summary>
    public static Rectangle ItemFrame(int slot)
    {
        ValidateItemSlot(slot);
        return new Rectangle(206, 138 + slot * 64, 54, 54);
    }

    public static Rectangle ItemFrameSource => new(414, 13, 54, 54);

    /// <summary>
    /// A recipient's card, which is also its half-open selection target, local
    /// (208, 15 + 36n)-(305, 49 + 36n) (SCR-GIVE-001, FND-GIVE-001, FND-GIVE-002).
    /// </summary>
    public static Rectangle RecipientHit(int slot)
    {
        ValidateRecipient(slot);
        return SharedPanelLayout.At(208, 15 + slot * 36, 97, 34);
    }

    public static Rectangle RecipientCardSource => new(0, 560, 97, 34);

    /// <summary>The recipient's portrait at half size; a double-click there opens its information.</summary>
    public static Rectangle RecipientPortrait(int slot)
    {
        ValidateRecipient(slot);
        return new Rectangle(313, 140 + slot * 36, 32, 32);
    }

    /// <summary>The Force meter: 6 pixels a point of the 3-row green strip at (354,0) of PX00129.</summary>
    public static Rectangle RecipientForce(int slot, int force)
    {
        ValidateRecipient(slot);
        return new Rectangle(347, 143 + slot * 36, ForceWidth(force), 3);
    }

    public static Rectangle RecipientForceSource(int force) => new(354, 0, ForceWidth(force), 3);

    public static Rectangle RecipientItem(int slot, int itemSlot)
    {
        ValidateRecipient(slot);
        ValidateItemSlot(itemSlot);
        return new Rectangle(346 + itemSlot * 21, 150 + slot * 36, 20, 20);
    }

    /// <summary>The arrow left of the chosen recipient's card (SCR-GIVE-001, FND-GIVE-002).</summary>
    public static Rectangle RecipientMarker(int slot)
    {
        ValidateRecipient(slot);
        return new Rectangle(274, 140 + slot * 36, 32, 32);
    }

    public static Rectangle RecipientMarkerSource => MovementLayout.ArrowSource(4);

    private static int ForceWidth(int force)
    {
        if (force < 0) throw new ArgumentOutOfRangeException(nameof(force));
        return 6 * force;
    }

    private static void ValidateItemSlot(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private static void ValidateRecipient(int slot)
    {
        if (slot is < 0 or >= RecipientCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }
}

public static class EquipmentGiveSelection
{
    /// <summary>
    /// The listed recipients: the player's other active gangs in the giver's sector, in roster
    /// order, at most five (SCR-GIVE-001, FND-GIVE-001).
    /// </summary>
    public static IReadOnlyList<GangId> Recipients(IEnumerable<MatchGangState> gangs, MatchGangState giver)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        ArgumentNullException.ThrowIfNull(giver);
        return gangs
            .Where(candidate => candidate.IsActive && candidate.Id != giver.Id
                && candidate.SectorId == giver.SectorId)
            .OrderBy(candidate => candidate.Id.Value)
            .Select(candidate => candidate.Id)
            .Take(EquipmentGiveLayout.RecipientCount)
            .ToArray();
    }

    /// <summary>
    /// The highest Tech Level among the selected items, starting from 0; a recipient whose gang
    /// type's Tech Level is lower is dimmed and cannot be chosen (FND-GIVE-001, FND-EQUIP-008).
    /// </summary>
    public static int RequiredTechLevel(IEnumerable<ItemDefinition> selectedItems)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        return selectedItems.Select(item => (int)item.TechLevel).DefaultIfEmpty(0).Max();
    }

    public static bool CanReceive(int recipientTechLevel, int requiredTechLevel) =>
        recipientTechLevel >= requiredTechLevel;

    /// <summary>
    /// The selection a panel opens with: the items and the recipient of the gang's Give order
    /// when it has one, otherwise nothing (FND-GIVE-001).
    /// </summary>
    public static (bool[] Items, GangId? Recipient) OpeningSelection(
        GameCommand? queued, IReadOnlyList<short?> carried)
    {
        ArgumentNullException.ThrowIfNull(carried);
        var items = new bool[3];
        if (queued is not { Action: GangAction.Give }) return (items, null);
        var given = queued.GiveTargets().Select(target => target.Id).ToHashSet();
        for (var slot = 0; slot < items.Length && slot < carried.Count; slot++)
            items[slot] = carried[slot] is { } item && given.Contains(item);
        return (items, new GangId(queued.Target.Id));
    }

    public static GameCommand CreateCommand(
        PlayerId player,
        GangId source,
        GangId recipient,
        IEnumerable<short> selectedItems,
        bool repeat = false)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        var targets = selectedItems.Distinct().Select(item => CommandTarget.Item(item)).ToArray();
        if (targets.Length == 0) throw new ArgumentException("At least one item must be selected.", nameof(selectedItems));
        if (targets.Length > 3) throw new ArgumentException("A gang has only three equipment slots.", nameof(selectedItems));
        return new GameCommand(
            player,
            source,
            GangAction.Give,
            CommandTarget.Gang(recipient),
            repeat,
            targets[0],
            targets.Length > 1 ? targets[1] : null,
            targets.Length > 2 ? targets[2] : null);
    }
}

public sealed partial class ChaosGame
{
    private IReadOnlyList<GangId> _giveRecipients = [];
    private Texture2D? _giveRecipientDimOverlay;

    private void OpenGiveEquipment(ClientScreen returnScreen, bool repeat = false)
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            RejectInput("GIVE REQUIRES THE COMMAND PHASE");
            return;
        }
        var player = _state.FindPlayer(playerId)!;
        var gang = SelectedGang(player);
        if (gang is null || EquippedItems(gang).All(item => item is null))
        {
            RejectInput("GANG HAS NO EQUIPMENT TO GIVE");
            return;
        }

        // SCR-GIVE-001: an existing Give order's items and recipient come back, with the face
        // enabled; otherwise nothing is selected.
        var queued = gang.QueuedCommand?.Command;
        var (items, recipient) = EquipmentGiveSelection.OpeningSelection(queued, EquippedItems(gang));
        items.CopyTo(_giveSelections, 0);
        _giveRecipients = EquipmentGiveSelection.Recipients(player.Gangs, gang);
        _giveCursor = recipient is { } chosen ? IndexOfGiveRecipient(chosen) : -1;
        _commandPanelFace = CommandPanelFaces.OnOpening(queued?.Action == GangAction.Give);
        _pressedCommandPanelButton = null;
        _commandPanelClicks.Cancel();
        _equipmentPortraitClicks.Cancel();
        _giveGang = gang.Id;
        _giveReturnScreen = returnScreen;
        _giveRepeats = repeat;
        _screens.Show(ClientScreen.Give);
    }

    private int IndexOfGiveRecipient(GangId gang)
    {
        for (var index = 0; index < _giveRecipients.Count; index++)
            if (_giveRecipients[index] == gang) return index;
        return -1;
    }

    private int GiveRequiredTechLevel()
    {
        if (_state is null || _giveGang is not { } gangId || _state.FindGang(gangId) is not { } gang) return 0;
        var carried = EquippedItems(gang);
        return EquipmentGiveSelection.RequiredTechLevel(Enumerable.Range(0, 3)
            .Where(slot => _giveSelections[slot] && carried[slot].HasValue)
            .Select(slot => _state.Definitions.Items[carried[slot]!.Value]));
    }

    private bool GiveRecipientEligible(int slot)
    {
        if (_state is null || slot < 0 || slot >= _giveRecipients.Count
            || _state.FindGang(_giveRecipients[slot]) is not { } recipient) return false;
        return EquipmentGiveSelection.CanReceive(
            _state.Definitions.Gang(recipient.DefinitionId).TechLevel, GiveRequiredTechLevel());
    }

    private bool GiveReady() => _giveSelections.Any(selected => selected) && GiveRecipientEligible(_giveCursor);

    private void ToggleGiveSelection(int slot)
    {
        if (slot < 0 || slot >= _giveSelections.Length) return;
        if (_state is null || _giveGang is not { } gangId) return;
        var gang = _state.FindGang(gangId);
        if (gang is null || EquippedItems(gang)[slot] is null) return;
        _giveSelections[slot] = !_giveSelections[slot];
        // FND-GIVE-001: a recipient whose Tech Level is now too low is dropped.
        if (!GiveRecipientEligible(_giveCursor)) _giveCursor = -1;
        _commandPanelFace = CommandPanelFaces.AfterChange(GiveReady());
    }

    private void ChooseGiveRecipient(int slot)
    {
        if (!GiveRecipientEligible(slot)) return;
        _giveCursor = slot;
        _commandPanelFace = CommandPanelFaces.AfterChange(GiveReady());
    }

    /// <summary>DEV-GIVE-001: Up and Down cycle through the recipients that can be chosen.</summary>
    private void MoveGiveCursor(int delta)
    {
        var count = _giveRecipients.Count;
        if (count == 0) return;
        var start = _giveCursor < 0 ? (delta < 0 ? 0 : count - 1) : _giveCursor;
        for (var step = 1; step <= count; step++)
        {
            var candidate = Mod(start + step * Math.Sign(delta), count);
            if (!GiveRecipientEligible(candidate)) continue;
            ChooseGiveRecipient(candidate);
            return;
        }
    }

    private void HandleGiveEquipmentClick(Point point)
    {
        if (CommandPanelFaces.ButtonAt(point) is { } button)
        {
            BeginCommandPanelButton(button);
            return;
        }
        if (!EquipmentGiveLayout.Panel.Contains(point))
        {
            RejectOutsideCommandPanel();
            return;
        }
        if (_state is null || _giveGang is not { } gangId || _state.FindGang(gangId) is not { } gang) return;
        if (EquipmentGiveLayout.Portrait.Contains(point))
        {
            if (_equipmentPortraitClicks.Register(0, _inputTime))
                OpenGangDetails(gang, ClientScreen.Give);
            return;
        }
        var carried = EquippedItems(gang);
        for (var slot = 0; slot < 3; slot++)
        {
            if (!EquipmentGiveLayout.ItemHit(slot).Contains(point)) continue;
            ToggleGiveSelection(slot);
            // SCR-GIVE-001: a double-click on an item cell opens SCR-UI-006 for the item.
            if (_commandPanelClicks.Register(slot, _inputTime) && carried[slot] is { } itemId)
                OpenItemDetails(itemId, ClientScreen.Give);
            return;
        }
        for (var slot = 0; slot < _giveRecipients.Count; slot++)
        {
            if (!EquipmentGiveLayout.RecipientHit(slot).Contains(point)) continue;
            var recipient = _state.FindGang(_giveRecipients[slot]);
            if (recipient is not null)
            {
                // Double-clicks on a card's portrait and item icons open their information.
                if (EquipmentGiveLayout.RecipientPortrait(slot).Contains(point))
                {
                    if (_commandPanelClicks.Register(10 + slot, _inputTime))
                    {
                        OpenGangDetails(recipient, ClientScreen.Give);
                        return;
                    }
                }
                else
                {
                    var recipientItems = EquippedItems(recipient);
                    for (var itemSlot = 0; itemSlot < 3; itemSlot++)
                    {
                        if (!EquipmentGiveLayout.RecipientItem(slot, itemSlot).Contains(point)) continue;
                        if (_commandPanelClicks.Register(20 + slot * 3 + itemSlot, _inputTime)
                            && recipientItems[itemSlot] is { } itemId)
                        {
                            OpenItemDetails(itemId, ClientScreen.Give);
                            return;
                        }
                        break;
                    }
                }
            }
            ChooseGiveRecipient(slot);
            return;
        }
    }

    private void QueueSelectedGive() => QueueSelectedGive(pointerButton: false);

    private void QueueSelectedGive(bool pointerButton)
    {
        if (!GiveReady())
        {
            RejectInput(_giveSelections.Any(selected => selected)
                ? "NO EQUIPMENT RECIPIENT SELECTED"
                : "NO EQUIPMENT SELECTED");
            return;
        }
        if (_actions is null || _state?.Coordinator.ActivePlayer is not { } playerId
            || _giveGang is not { } gangId || _state.FindGang(gangId) is not { } gang) return;
        var carried = EquippedItems(gang);
        var selected = Enumerable.Range(0, 3)
            .Where(slot => _giveSelections[slot] && carried[slot].HasValue)
            .Select(slot => carried[slot]!.Value);
        var command = EquipmentGiveSelection.CreateCommand(
            playerId, gangId, _giveRecipients[_giveCursor], selected, _giveRepeats);
        var result = _actions.Submit(command);
        ReportButtonResult(result.Accepted, result.Validation.Message, pointerButton);
        if (result.Accepted) _screens.Show(_giveReturnScreen);
    }

    private void CloseGiveEquipment() => _screens.Show(_giveReturnScreen);

    private void DrawGiveEquipment(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_giveReturnScreen == ClientScreen.Items) DrawItems(batch, pixel, font, state);
        else DrawMapBackdrop(batch, pixel, font, state, _giveReturnScreen);
        DrawGivePanel(batch, pixel, state);
    }

    /// <summary>SCR-GIVE-001: the recipient cards, the marks, then the item animation.</summary>
    private void DrawGivePanel(SpriteBatch batch, Texture2D pixel, MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _equipmentGiveBackground, EquipmentGiveLayout.Panel, 248);

        if (_giveGang is not { } gangId || state.FindGang(gangId) is not { } gang) return;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentGiveLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);

        for (var slot = 0; slot < _giveRecipients.Count; slot++)
            if (state.FindGang(_giveRecipients[slot]) is { } recipient)
                DrawGiveRecipient(batch, state, slot, recipient);

        var equipped = EquippedItems(gang);
        if (_uiKeyedSprites is not null)
        {
            for (var slot = 0; slot < equipped.Length; slot++)
                if (_giveSelections[slot] && equipped[slot] is not null)
                    batch.Draw(_uiKeyedSprites, EquipmentGiveLayout.ItemFrame(slot),
                        EquipmentGiveLayout.ItemFrameSource, Color.White);
            if (_giveCursor >= 0 && _giveCursor < _giveRecipients.Count)
                batch.Draw(_uiKeyedSprites, EquipmentGiveLayout.RecipientMarker(_giveCursor),
                    EquipmentGiveLayout.RecipientMarkerSource, Color.White);
        }
        for (var slot = 0; slot < equipped.Length; slot++)
            if (equipped[slot] is { } itemId)
                DrawItemRotation(batch, itemId, EquipmentGiveLayout.ItemPicture(slot));
        DrawCommandPanelFaces(batch);
    }

    /// <summary>
    /// A recipient card: the card art, the half-size portrait, the Force meter and the three
    /// item icons, dimmed by black through bitmap 146 when the gang type's Tech Level is below
    /// the selected items' (SCR-GIVE-001, FND-GIVE-002, FND-GIVE-003).
    /// </summary>
    private void DrawGiveRecipient(SpriteBatch batch, MatchState state, int slot, MatchGangState recipient)
    {
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, EquipmentGiveLayout.RecipientHit(slot),
                EquipmentGiveLayout.RecipientCardSource, Color.White);
            if (recipient.Force > 0)
                batch.Draw(_uiSprites, EquipmentGiveLayout.RecipientForce(slot, recipient.Force),
                    EquipmentGiveLayout.RecipientForceSource(recipient.Force), Color.White);
        }
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentGiveLayout.RecipientPortrait(slot),
                OriginalSpriteLayout.GangPortrait(recipient.DefinitionId), Color.White);
        if (_itemPortraits is not null)
        {
            var items = EquippedItems(recipient);
            for (var itemSlot = 0; itemSlot < items.Length; itemSlot++)
                if (items[itemSlot] is { } itemId)
                    batch.Draw(_itemPortraits, EquipmentGiveLayout.RecipientItem(slot, itemSlot),
                        OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
        }
        if (!GiveRecipientEligible(slot))
        {
            var card = EquipmentGiveLayout.RecipientHit(slot);
            if (_giveRecipientDimOverlay is null)
            {
                _giveRecipientDimOverlay = new Texture2D(GraphicsDevice, card.Width, card.Height);
                _giveRecipientDimOverlay.SetData(OriginalPatternMask.ShadedRectangle(
                    EquipmentGiveLayout.IneligibleCardPattern, card.Width, card.Height,
                    Color.Black, Color.Black));
            }
            batch.Draw(_giveRecipientDimOverlay, card, Color.White);
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class EquipmentSellLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    /// <summary>The carried item's animated 48-by-48 picture (SCR-SELL-001, FND-SELL-001).</summary>
    public static Rectangle ItemPicture(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(217, 141 + slot * 64, 48, 48);
    }

    /// <summary>The black fill of an empty row's text area (SCR-SELL-001, FND-SELL-001).</summary>
    public static Rectangle EmptyRow(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(272, 149 + slot * 64, 125, 32);
    }

    public static Point NameOrigin(int slot)
    {
        ValidateSlot(slot);
        return new Point(272, 156 + slot * 64);
    }

    /// <summary>Left edge and row of the two-cell sale price field (SCR-SELL-001).</summary>
    public static Point PriceField(int slot)
    {
        ValidateSlot(slot);
        return new Point(386, 174 + slot * 64);
    }

    /// <summary>
    /// The selection highlight, one pixel outside the row's input rectangle
    /// (SCR-SELL-001, FND-SELL-002).
    /// </summary>
    public static Rectangle Highlight(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(214, 138 + slot * 64, 192, 54);
    }

    /// <summary>PX00129 art of the highlight, keyed on exact white (FND-SELL-002).</summary>
    public static Rectangle HighlightSource => new(222, 363, 192, 54);

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
    }

    /// <summary>
    /// Native Sell handler 0x00443bbd's half-open item-toggle target. It is
    /// narrower than the row artwork, which also reserves a price column.
    /// </summary>
    public static Rectangle ItemHit(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(111, 15 + slot * 64, 190, 52);
    }
}

public static class EquipmentSellSelection
{
    /// <summary>
    /// The selection a panel opens with: the items of the gang's Sell order when it has one,
    /// otherwise none (FND-SELL-001).
    /// </summary>
    public static bool[] OpeningSelection(GameCommand? queued, IReadOnlyList<short?> carried)
    {
        ArgumentNullException.ThrowIfNull(carried);
        var selection = new bool[3];
        if (queued is not { Action: GangAction.Sell }) return selection;
        var items = queued.SellTargets().Select(target => target.Id).ToHashSet();
        for (var slot = 0; slot < selection.Length && slot < carried.Count; slot++)
            selection[slot] = carried[slot] is { } item && items.Contains(item);
        return selection;
    }

    public static GameCommand CreateCommand(
        PlayerId player,
        GangId gang,
        IEnumerable<short> selectedItems,
        bool repeat = false)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        var targets = selectedItems.Distinct().Select(item => CommandTarget.Item(item)).ToArray();
        if (targets.Length == 0) throw new ArgumentException("At least one item must be selected.", nameof(selectedItems));
        if (targets.Length > 3) throw new ArgumentException("A gang has only three equipment slots.", nameof(selectedItems));
        return new GameCommand(
            player,
            gang,
            GangAction.Sell,
            targets[0],
            repeat,
            targets.Length > 1 ? targets[1] : null,
            targets.Length > 2 ? targets[2] : null);
    }
}

public sealed partial class ChaosGame
{
    private void OpenSellEquipment(ClientScreen returnScreen, bool repeat = false)
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            RejectInput("SELL REQUIRES THE COMMAND PHASE");
            return;
        }
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null || EquippedItems(gang).All(item => item is null))
        {
            RejectInput("GANG HAS NO EQUIPMENT TO SELL");
            return;
        }

        // SCR-SELL-001: an existing Sell order's selection comes back, with the face enabled.
        var queued = gang.QueuedCommand?.Command;
        EquipmentSellSelection.OpeningSelection(queued, EquippedItems(gang)).CopyTo(_sellSelections, 0);
        _commandPanelFace = CommandPanelFaces.OnOpening(queued?.Action == GangAction.Sell);
        _pressedCommandPanelButton = null;
        _commandPanelClicks.Cancel();
        _equipmentPortraitClicks.Cancel();
        _sellGang = gang.Id;
        _sellReturnScreen = returnScreen;
        _sellRepeats = repeat;
        _screens.Show(ClientScreen.Sell);
    }

    private void ToggleSellSelection(int slot)
    {
        if (slot < 0 || slot >= _sellSelections.Length) return;
        if (_state is null || _sellGang is not { } gangId) return;
        var gang = _state.FindGang(gangId);
        if (gang is null || EquippedItems(gang)[slot] is null) return;
        _sellSelections[slot] = !_sellSelections[slot];
        _commandPanelFace = CommandPanelFaces.AfterChange(_sellSelections.Any(selected => selected));
    }

    private void HandleSellClick(Point point)
    {
        if (CommandPanelFaces.ButtonAt(point) is { } button)
        {
            BeginCommandPanelButton(button);
            return;
        }
        if (!EquipmentSellLayout.Panel.Contains(point))
        {
            RejectOutsideCommandPanel();
            return;
        }
        if (_state is null || _sellGang is not { } gangId || _state.FindGang(gangId) is not { } gang) return;
        if (EquipmentSellLayout.Portrait.Contains(point))
        {
            // SCR-SELL-001: a double-click on the portrait opens the gang's information.
            if (_equipmentPortraitClicks.Register(0, _inputTime))
                OpenGangDetails(gang, ClientScreen.Sell);
            return;
        }
        var carried = EquippedItems(gang);
        for (var slot = 0; slot < 3; slot++)
        {
            if (!EquipmentSellLayout.ItemHit(slot).Contains(point)) continue;
            ToggleSellSelection(slot);
            // A double-click on a row with an item opens SCR-UI-006 for it.
            if (_commandPanelClicks.Register(slot, _inputTime) && carried[slot] is { } itemId)
                OpenItemDetails(itemId, ClientScreen.Sell);
            return;
        }
    }

    private void QueueSelectedSale() => QueueSelectedSale(pointerButton: false);

    private void QueueSelectedSale(bool pointerButton)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || _sellGang is not { } gangId || _actions is null) return;
        var gang = _state.FindGang(gangId);
        if (gang is null) return;
        var equipped = EquippedItems(gang);
        var selected = Enumerable.Range(0, 3)
            .Where(slot => _sellSelections[slot] && equipped[slot].HasValue)
            .Select(slot => equipped[slot]!.Value)
            .ToArray();
        if (selected.Length == 0)
        {
            RejectInput("NO EQUIPMENT SELECTED");
            return;
        }

        var result = _actions.Submit(EquipmentSellSelection.CreateCommand(
            playerId, gangId, selected, _sellRepeats));
        ReportButtonResult(result.Accepted, result.Validation.Message, pointerButton);
        if (result.Accepted) _screens.Show(_sellReturnScreen);
    }

    private void CloseSellEquipment() => _screens.Show(_sellReturnScreen);

    private void DrawSellEquipment(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_sellReturnScreen == ClientScreen.Items) DrawItems(batch, pixel, font, state);
        else DrawMapBackdrop(batch, pixel, font, state, _sellReturnScreen);
        DrawSellPanel(batch, pixel, font, state);
    }

    /// <summary>
    /// SCR-SELL-001: names and half prices over the panel, then the highlights, then the item
    /// animation, which the original copies over the highlight on every frame (FND-SELL-002).
    /// </summary>
    private void DrawSellPanel(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _equipmentSellBackground, EquipmentSellLayout.Panel, 248);

        if (_sellGang is not { } gangId || state.FindGang(gangId) is not { } gang) return;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentSellLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        var equipped = EquippedItems(gang);
        for (var slot = 0; slot < equipped.Length; slot++)
        {
            if (equipped[slot] is not { } itemId)
            {
                batch.Draw(pixel, EquipmentSellLayout.EmptyRow(slot), Color.Black);
                continue;
            }
            ItemDefinition item = state.Definitions.Items[itemId];
            font.Draw(batch, item.Name, EquipmentSellLayout.NameOrigin(slot).ToVector2(), Color.Lime, 1);
            var price = EquipmentSellLayout.PriceField(slot);
            DrawNativeTwoCellValue(font, batch, EquipmentRules.SaleValue(item), price.X, price.Y,
                NativeTwoCellNumberPresentation.Kind.Baseline);
        }
        for (var slot = 0; slot < equipped.Length; slot++)
            if (_sellSelections[slot] && _uiKeyedSprites is not null)
                batch.Draw(_uiKeyedSprites, EquipmentSellLayout.Highlight(slot),
                    EquipmentSellLayout.HighlightSource, Color.White);
        for (var slot = 0; slot < equipped.Length; slot++)
            if (equipped[slot] is { } itemId)
                DrawItemRotation(batch, itemId, EquipmentSellLayout.ItemPicture(slot));
        DrawCommandPanelFaces(batch);
    }

    /// <summary>The current 48-by-48 frame of an item's 15-frame strip.</summary>
    private void DrawItemRotation(SpriteBatch batch, short itemId, Rectangle destination)
    {
        if (itemId >= 0 && itemId < _itemRotationTextures.Length
            && _itemRotationTextures[itemId] is { } rotation)
            batch.Draw(rotation, destination, ItemRotationPresentation.Frame(_inputTime), Color.White);
    }

    private static short?[] EquippedItems(MatchGangState gang) =>
        [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
}

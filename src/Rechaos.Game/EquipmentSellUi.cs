using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class EquipmentSellLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle ItemRow(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(212, 141 + slot * 64, 220, 51);
    }

    public static int NameY(int slot) => ItemRow(slot).Y + 13;
    public static int PriceY(int slot) => ItemRow(slot).Y + 37;
}

public static class EquipmentSellSelection
{
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
            _message = "SELL REQUIRES THE COMMAND PHASE";
            return;
        }
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null || EquippedItems(gang).All(item => item is null))
        {
            _message = "GANG HAS NO EQUIPMENT TO SELL";
            return;
        }

        Array.Fill(_sellSelections, false);
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
    }

    private void HandleSellClick(Point point)
    {
        if (EquipmentSellLayout.Cancel.Contains(point)) CloseSellEquipment();
        else if (EquipmentSellLayout.Ok.Contains(point)) QueueSelectedSale();
        else
        {
            for (var slot = 0; slot < 3; slot++)
                if (EquipmentSellLayout.ItemRow(slot).Contains(point))
                {
                    ToggleSellSelection(slot);
                    return;
                }
        }
    }

    private void QueueSelectedSale()
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
            _message = "SELECT EQUIPMENT TO SELL";
            return;
        }

        var result = _actions.Submit(EquipmentSellSelection.CreateCommand(
            playerId, gangId, selected, _sellRepeats));
        _message = result.Accepted
            ? $"{selected.Length} ITEM{(selected.Length == 1 ? string.Empty : "S")} TO SELL QUEUED"
            : result.Validation.Message.ToUpperInvariant();
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
        else if (_sellReturnScreen == ClientScreen.Sector) DrawSectorDetails(batch, pixel, font, state);
        else DrawBoard(batch, pixel, font, state);
        if (_equipmentSellBackground is not null)
            batch.Draw(_equipmentSellBackground, EquipmentSellLayout.Panel, Color.White);
        else
            batch.Draw(pixel, EquipmentSellLayout.Panel, new Color(0, 0, 0, 248));

        if (_sellGang is not { } gangId || state.FindGang(gangId) is not { } gang) return;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentSellLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        var equipped = EquippedItems(gang);
        for (var slot = 0; slot < equipped.Length; slot++)
        {
            var row = EquipmentSellLayout.ItemRow(slot);
            batch.Draw(pixel, new Rectangle(row.X + 4, row.Y + 11, row.Width - 8, 9), Color.Black);
            batch.Draw(pixel, new Rectangle(row.Right - 48, row.Y + 35, 42, 9), Color.Black);
            if (equipped[slot] is not { } itemId) continue;
            ItemDefinition item = state.Definitions.Items[itemId];
            font.Draw(batch, item.Name, new Vector2(row.X + 7, EquipmentSellLayout.NameY(slot)), Color.Lime, 1);
            var price = EquipmentRules.SaleValue(item).ToString();
            font.Draw(batch, price, new Vector2(row.Right - 9 - price.Length * OriginalFontLayout.CellWidth,
                EquipmentSellLayout.PriceY(slot)), Color.Lime, 1);
            if (_sellSelections[slot]) DrawBorder(batch, pixel, row, Color.White, 2);
        }
    }

    private static short?[] EquippedItems(MatchGangState gang) =>
        [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
}

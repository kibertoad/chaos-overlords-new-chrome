using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class EquipmentGiveLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle Item(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(208, 141 + slot * 64, 50, 51);
    }

    public static int InformationY(int slot) => Item(slot).Y + 19;
}

public static class EquipmentGiveSelection
{
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
    private void OpenGiveEquipment(ClientScreen returnScreen, bool repeat = false)
    {
        if (_state is null || _state.Coordinator.Phase != TurnPhase.Command
            || _state.Coordinator.ActivePlayer is not { } playerId)
        {
            RejectInput("GIVE REQUIRES THE COMMAND PHASE");
            return;
        }
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        if (gang is null || GiveEquippedItems(gang).All(item => item is null))
        {
            RejectInput("GANG HAS NO EQUIPMENT TO GIVE");
            return;
        }

        Array.Fill(_giveSelections, false);
        _giveGang = gang.Id;
        _giveReturnScreen = returnScreen;
        _giveRepeats = repeat;
        _screens.Show(ClientScreen.Give);
    }

    private void ToggleGiveSelection(int slot)
    {
        if (slot < 0 || slot >= _giveSelections.Length) return;
        if (_state is null || _giveGang is not { } gangId) return;
        var gang = _state.FindGang(gangId);
        if (gang is null || GiveEquippedItems(gang)[slot] is null) return;
        _giveSelections[slot] = !_giveSelections[slot];
    }

    private void HandleGiveEquipmentClick(Point point)
    {
        if (EquipmentGiveLayout.Cancel.Contains(point)) CloseGiveEquipment();
        else if (EquipmentGiveLayout.Ok.Contains(point)) OpenGiveTargets();
        else
        {
            for (var slot = 0; slot < 3; slot++)
                if (EquipmentGiveLayout.Item(slot).Contains(point))
                {
                    ToggleGiveSelection(slot);
                    return;
                }
        }
    }

    private void CloseGiveEquipment() => _screens.Show(_giveReturnScreen);

    private void DrawGiveEquipment(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_giveReturnScreen == ClientScreen.Items) DrawItems(batch, pixel, font, state);
        else if (_giveReturnScreen == ClientScreen.Sector) DrawSectorDetails(batch, pixel, font, state);
        else DrawBoard(batch, pixel, font, state);
        if (_equipmentGiveBackground is not null)
            batch.Draw(_equipmentGiveBackground, EquipmentGiveLayout.Panel, Color.White);
        else
            batch.Draw(pixel, EquipmentGiveLayout.Panel, new Color(0, 0, 0, 248));

        if (_giveGang is not { } gangId || state.FindGang(gangId) is not { } gang) return;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, EquipmentGiveLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
        var equipped = GiveEquippedItems(gang);
        for (var slot = 0; slot < equipped.Length; slot++)
        {
            var aperture = EquipmentGiveLayout.Item(slot);
            if (equipped[slot] is not { } itemId) continue;
            if (_itemPortraits is not null)
                batch.Draw(_itemPortraits,
                    new Rectangle(aperture.X + 5, aperture.Y + 5, 40, 40),
                    OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
            var item = state.Definitions.Items[itemId];
            font.Draw(batch, item.Name, new Vector2(270, EquipmentGiveLayout.InformationY(slot)),
                Color.Lime, 1);
            if (_giveSelections[slot]) DrawBorder(batch, pixel, aperture, Color.White, 2);
        }
    }

    private static short?[] GiveEquippedItems(MatchGangState gang) =>
        [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
}

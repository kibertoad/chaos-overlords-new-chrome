using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle ItemsResearch = new(10, 414, 96, 28);
    private static readonly Rectangle ItemsEquip = new(114, 414, 96, 28);
    private static readonly Rectangle ItemsGive = new(218, 414, 96, 28);
    private static readonly Rectangle ItemsSell = new(322, 414, 96, 28);
    private static readonly Rectangle ItemsBack = new(426, 414, 96, 28);

    private void OpenItems()
    {
        if (_state is null) return;
        var items = RealItems(_state);
        _itemCursor = Math.Clamp(_itemCursor, 0, Math.Max(0, items.Length - 1));
        _screens.Show(ClientScreen.Items);
    }

    private void MoveItemCursor(int delta)
    {
        if (_state is null) return;
        var count = RealItems(_state).Length;
        if (count > 0) _itemCursor = Mod(_itemCursor + delta, count);
    }

    private void HandleItemsClick(Point point)
    {
        if (_state is null) return;
        if (point.X is >= 14 and < 330 && point.Y is >= 108 and < 396)
        {
            var items = RealItems(_state);
            var first = Math.Max(0, _itemCursor - 8);
            var index = first + (point.Y - 108) / 16;
            if (index < items.Length) _itemCursor = index;
        }
        else if (ItemsResearch.Contains(point)) QueueItemCommand(GangAction.Research);
        else if (ItemsEquip.Contains(point)) QueueItemCommand(GangAction.Equip);
        else if (ItemsGive.Contains(point)) OpenGiveEquipment(ClientScreen.Items);
        else if (ItemsSell.Contains(point)) OpenSellEquipment(ClientScreen.Items);
        else if (ItemsBack.Contains(point)) _screens.Show(ClientScreen.City);
    }

    private void QueueItemCommand(GangAction action)
    {
        if (_state is null || _actions is null) return;
        var playerId = ViewingPlayer(_state);
        var gang = SelectedGang(_state.FindPlayer(playerId)!);
        var items = RealItems(_state);
        if (gang is null || items.Length == 0)
        {
            RejectInput("NO ACTIVE GANG OR ITEM");
            return;
        }
        var command = new GameCommand(playerId, gang.Id, action, CommandTarget.Item(items[_itemCursor].Id));
        var result = _actions.Submit(command);
        ReportInputResult(result.Accepted, result.Validation.Message);
        if (result.Accepted) _screens.Show(ClientScreen.City);
    }

    private void DrawItems(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 624, 402), new Color(0, 0, 0, 240));
        var playerId = ViewingPlayer(state);
        var player = state.FindPlayer(playerId)!;
        var gang = SelectedGang(player);
        var items = RealItems(state);
        font.Draw(batch, "RESEARCH AND EQUIPMENT", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, gang is null ? "NO ACTIVE GANG" :
            $"{state.Definitions.Gang(gang.DefinitionId).Name}  CASH ${player.Cash}",
            new Vector2(18, 86), PlayerColors[playerId.Value], 1);
        if (gang is not null)
        {
            var portrait = GangArtLayout.SelectedEquipmentPortrait;
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, portrait,
                    OriginalSpriteLayout.GangPortrait(gang.DefinitionId), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[playerId.Value], 1);
        }

        var first = Math.Max(0, _itemCursor - 8);
        foreach (var entry in items.Skip(first).Take(18).Select((item, index) => (item, index)))
        {
            var itemIndex = first + entry.index;
            var y = 108 + entry.index * 16;
            if (itemIndex == _itemCursor)
                batch.Draw(pixel, new Rectangle(14, y - 3, 316, 14), new Color(72, 54, 18));
            var marker = player.ResearchedItems.Contains(entry.item.Id) || entry.item.ResearchDifficulty == 0
                ? "+"
                : player.ResearchProgress.ContainsKey(entry.item.Id) ? ">" : " ";
            font.Draw(batch, $"{marker} {entry.item.Name}", new Vector2(18, y), Color.White, 1);
        }

        if (items.Length > 0)
        {
            var item = items[_itemCursor];
            var remaining = player.RemainingResearch(state.Definitions, item.Id);
            var researched = remaining == 0 ? "COMPLETE" : $"{remaining} REMAIN";
            var equipmentCost = gang is null ? item.Cost : SpecialSiteRules.EquipmentCost(state, gang, item);
            font.Draw(batch, item.Name, new Vector2(350, 112), Color.Gold, 1);
            font.Draw(batch, $"{EquipmentRules.SlotFor(item).ToString().ToUpperInvariant()}  TECH {item.TechLevel}",
                new Vector2(350, 136), Color.White, 1);
            font.Draw(batch, $"COST ${equipmentCost}" + (equipmentCost < item.Cost ? "  FACTORY" : ""),
                new Vector2(350, 152), Color.White, 1);
            font.Draw(batch, $"RESEARCH {researched}", new Vector2(350, 168), Color.White, 1);
            if (gang is not null)
                font.Draw(batch, $"TECH LIMIT {SpecialSiteRules.ResearchTechLimit(state, gang)}",
                    new Vector2(350, 184), Color.White, 1);
            font.Draw(batch, "MODIFIERS", new Vector2(350, 202), new Color(180, 230, 170), 1);
            var modifiers = ItemModifiers(item).ToArray();
            for (var index = 0; index < modifiers.Length; index++)
                font.Draw(batch, modifiers[index], new Vector2(350, 220 + index * 16), Color.White, 1);
            if (gang is not null)
            {
                var equipped = EquipmentRules.EquippedItem(gang, EquipmentRules.SlotFor(item));
                font.Draw(batch, equipped == item.Id ? "EQUIPPED" : "NOT EQUIPPED",
                    new Vector2(350, 348), equipped == item.Id ? Color.Lime : Color.White, 1);
            }
        }
        font.Draw(batch, "UP/DOWN ITEM  LEFT/RIGHT GANG", new Vector2(18, 395), Color.White, 1);
        DrawButton(batch, pixel, font, ItemsResearch, "RESEARCH", false);
        DrawButton(batch, pixel, font, ItemsEquip, "EQUIP", false);
        DrawButton(batch, pixel, font, ItemsGive, "GIVE", false);
        DrawButton(batch, pixel, font, ItemsSell, "SELL", false);
        DrawButton(batch, pixel, font, ItemsBack, "BACK", false);
    }

    private static ItemDefinition[] RealItems(MatchState state) =>
        state.Definitions.Items.Where(item => item.Type != 99).OrderBy(item => item.Id).ToArray();

    private static IEnumerable<string> ItemModifiers(ItemDefinition item)
    {
        var stats = item.Stats;
        (string Name, int Value)[] values =
        [
            ("COMBAT", stats.Combat), ("DEFENSE", stats.Defense), ("STEALTH", stats.Stealth),
            ("DETECT", stats.Detect), ("CHAOS", stats.Chaos), ("CONTROL", stats.Control),
            ("HEAL", stats.Heal), ("INFLUENCE", stats.Influence), ("RESEARCH", stats.Research),
            ("STRENGTH", stats.Strength), ("BLADE", stats.Blade), ("RANGE", stats.Range),
            ("FIGHTING", stats.Fighting), ("M ARTS", stats.MartialArts)
        ];
        var any = false;
        foreach (var value in values.Where(value => value.Value != 0).Take(8))
        {
            any = true;
            yield return $"{value.Name} {(value.Value > 0 ? "+" : "")}{value.Value}";
        }
        if (!any) yield return "NONE";
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void OpenSectorGangDetails(ClientScreen returnScreen) =>
        OpenSectorGangs(returnScreen);

    private void OpenGangDetails(
        MatchGangState gang,
        ClientScreen returnScreen,
        int? sectorFilter = null)
    {
        _gangDetailsInstanceId = gang.Id;
        _gangDetailsDefinitionId = gang.DefinitionId;
        _gangDetailsReturnScreen = returnScreen;
        _gangDetailsSectorFilter = sectorFilter;
        _screens.Show(ClientScreen.Gang);
    }

    private void OpenGangDefinitionDetails(short definitionId, ClientScreen returnScreen)
    {
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = definitionId;
        _gangDetailsReturnScreen = returnScreen;
        _gangDetailsSectorFilter = null;
        _screens.Show(ClientScreen.Gang);
    }

    private void CloseGangDetails()
    {
        var returnScreen = _gangDetailsReturnScreen;
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = null;
        _gangDetailsSectorFilter = null;
        _screens.Show(returnScreen);
    }

    private void CycleGangDetails(int delta)
    {
        if (_gangDetailsSectorFilter is not { } sectorId
            || _state?.Coordinator.ActivePlayer is not { } playerId)
        {
            CycleGang(delta);
            return;
        }
        var gangs = GangInformationRoster.ForSector(
            _state.FindPlayer(playerId)!.Gangs, sectorId);
        if (gangs.Count == 0) return;
        var current = _gangDetailsInstanceId is { } id
            ? Enumerable.Range(0, gangs.Count).FirstOrDefault(index => gangs[index].Id == id, -1)
            : -1;
        var gang = gangs[Mod(current + delta, gangs.Count)];
        _gangDetailsInstanceId = gang.Id;
        _gangDetailsDefinitionId = gang.DefinitionId;
        _selectedGangIndex = _state.FindPlayer(playerId)!.Gangs
            .Where(candidate => candidate.IsActive)
            .Select((candidate, index) => (candidate, index))
            .First(entry => entry.candidate.Id == gang.Id).index;
        _message = $"GANG {gang.Id.Value}";
    }

    private void DrawGangDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawGangDetailsBackdrop(batch, pixel, font, state);
        DrawGangDetailsPanel(batch, pixel, font, state);
    }

    private void DrawGangDetailsBackdrop(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_gangDetailsReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(batch, pixel, font, state);
        else
            DrawBoard(batch, pixel, font, state);
    }

    private void DrawGangDetailsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        var panel = GangInformationLayout.Panel;
        var background = _gangDetailsInstanceId is null
            ? _gangDefinitionInfoBackground
            : _gangInfoBackground;
        if (background is not null)
            batch.Draw(background, panel, Color.White);
        else
            batch.Draw(pixel, panel, new Color(0, 0, 0, 245));
        var gang = _gangDetailsInstanceId is { } instanceId ? state.FindGang(instanceId) : null;
        var definitionId = gang?.DefinitionId ?? _gangDetailsDefinitionId;
        if (definitionId is null)
        {
            font.Draw(batch, "NO ACTIVE GANG", new Vector2(200, 153), Color.White, 1);
        }
        else
        {
            var definition = state.Definitions.Gangs.Single(value => value.Id == definitionId.Value);
            var stats = gang is null || _showBaseStatistics
                ? EffectiveStatistics.From(definition.Stats)
                : EffectiveStatisticsCalculator.ForGang(state, gang);
            ClearGangInformationFields(batch, pixel);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, GangInformationLayout.Portrait,
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            if (gang is not null && _itemPortraits is not null)
            {
                short?[] equipment =
                    [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
                for (var slot = 0; slot < equipment.Length; slot++)
                    if (equipment[slot] is { } itemId)
                        batch.Draw(_itemPortraits, GangInformationLayout.Equipment(slot),
                            OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
            }
            font.Draw(batch, definition.Name, new Vector2(202, 153), Color.Lime, 1);
            foreach (var entry in WrapPanelText(definition.Description, 27).Take(3).Select((text, row) => (text, row)))
                font.Draw(batch, entry.text, new Vector2(202, 170 + entry.row * 10), Color.Lime, 1);
            DrawPanelValue(font, batch, gang is null ? "??" : gang.Force.ToString(),
                GangInformationLayout.LeftValueRight, 217);
            DrawPanelValue(font, batch, definition.Upkeep, GangInformationLayout.RightValueRight, 217);
            DrawPanelValue(font, batch, definition.TechLevel, GangInformationLayout.RightValueRight, 226);
            int[] left = [stats.Combat, stats.Defense, stats.Chaos, stats.Control, stats.Heal, stats.Influence, stats.Research];
            int[] right = [stats.Stealth, stats.Detect, stats.Strength, stats.Blade, stats.Range, stats.Fighting, stats.MartialArts];
            for (var index = 0; index < left.Length; index++)
            {
                var y = GangInformationLayout.StatisticY(index);
                DrawPanelValue(font, batch, left[index], GangInformationLayout.LeftValueRight, y);
                DrawPanelValue(font, batch, right[index], GangInformationLayout.RightValueRight, y);
            }
        }
    }

    private static void ClearGangInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(200, 152, 180, 10), Color.Black);
        batch.Draw(pixel, new Rectangle(200, 169, 186, 37), Color.Black);
        batch.Draw(pixel, new Rectangle(276, 217, 12, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(372, 217, 12, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(372, 226, 12, 7), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = GangInformationLayout.StatisticY(row);
            // PX05000 reserves exactly two six-pixel glyph cells for each value.
            // Keep the adjacent green dividers intact: widening these masks makes
            // their surviving pixels look like punctuation beside the new value.
            batch.Draw(pixel, new Rectangle(276, y, 12, 7), Color.Black);
            batch.Draw(pixel, new Rectangle(372, y, 12, 7), Color.Black);
        }
    }

    private static IEnumerable<string> WrapPanelText(string text, int width)
    {
        var remaining = text.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        foreach (var word in remaining)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : line + " " + word;
            }
        }
        if (line.Length > 0) yield return line;
    }

    private static void DrawPanelValue(PixelFont font, SpriteBatch batch, int value, int right, int y)
        => DrawPanelValue(font, batch, value.ToString(), right, y);

    private static void DrawPanelValue(PixelFont font, SpriteBatch batch, string text, int right, int y)
    {
        DrawPanelValue(font, batch, text, right, y, Color.Lime);
    }

    private static void DrawPanelValue(
        PixelFont font, SpriteBatch batch, string text, int right, int y, Color color) =>
        font.Draw(batch, text, new Vector2(right - text.Length * 6, y), color, 1);
}

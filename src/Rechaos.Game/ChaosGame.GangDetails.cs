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
        _gangEquipmentItemClicks.Cancel();
        _screens.Show(ClientScreen.Gang);
    }

    private void OpenGangDefinitionDetails(short definitionId, ClientScreen returnScreen)
    {
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = definitionId;
        _gangDetailsReturnScreen = returnScreen;
        _gangDetailsSectorFilter = null;
        _gangEquipmentItemClicks.Cancel();
        _screens.Show(ClientScreen.Gang);
    }

    private void CloseGangDetails()
    {
        var returnScreen = _gangDetailsReturnScreen;
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = null;
        _gangDetailsSectorFilter = null;
        _gangEquipmentItemClicks.Cancel();
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
        _gangEquipmentItemClicks.Cancel();
        _selectedGangIndex = _state.FindPlayer(playerId)!.Gangs
            .Where(candidate => candidate.IsActive)
            .Select((candidate, index) => (candidate, index))
            .First(entry => entry.candidate.Id == gang.Id).index;
        _message = string.Empty;
    }

    private void DrawGangDetails(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawGangDetailsBackdrop(batch, pixel, font, state);
        DrawGangDetailsPanel(batch, pixel, font, state);
    }

    private bool HandleGangDetailsEquipmentClick(Point point)
    {
        if (_gangDetailsInstanceId is not { } gangId
            || _state?.FindGang(gangId) is not { } gang
            || GangInformationLayout.EquipmentSlotAt(point) is not { } slot)
            return false;

        var itemId = EquippedItem(gang, slot);
        if (itemId is { } resolved
            && _gangEquipmentItemClicks.Register(slot, _inputTime))
            OpenItemDetails(resolved, ClientScreen.Gang);
        return true;
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
        var definitionOnly = _gangDetailsInstanceId is null;
        var panel = definitionOnly ? GangDefinitionInformationLayout.Panel : GangInformationLayout.Panel;
        var background = definitionOnly
            ? _gangDefinitionInfoBackground
            : _gangInfoBackground;
        if (background is not null)
        {
            if (definitionOnly)
                batch.Draw(background, panel, GangDefinitionInformationLayout.BackgroundSource, Color.White);
            else
                batch.Draw(background, panel, Color.White);
        }
        else
            batch.Draw(pixel, panel, new Color(0, 0, 0, 245));
        var gang = _gangDetailsInstanceId is { } instanceId ? state.FindGang(instanceId) : null;
        var definitionId = gang?.DefinitionId ?? _gangDetailsDefinitionId;
        if (definitionId is null)
        {
            font.Draw(batch, "NO ACTIVE GANG",
                new Vector2(definitionOnly ? GangDefinitionInformationLayout.NameLeft : SharedPanelLayout.X(96),
                    definitionOnly ? GangDefinitionInformationLayout.NameY : SharedPanelLayout.Y(28)), Color.White, 1);
        }
        else
        {
            var definition = state.Definitions.Gangs.Single(value => value.Id == definitionId.Value);
            var stats = gang is null || _showBaseStatistics
                ? EffectiveStatistics.From(definition.Stats)
                : EffectiveStatisticsCalculator.ForGang(state, gang);
            ClearGangInformationFields(batch, pixel, definitionOnly);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, definitionOnly
                    ? GangDefinitionInformationLayout.Portrait
                    : GangInformationLayout.Portrait,
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            if (gang is not null && _itemPortraits is not null)
            {
                for (var slot = 0; slot < 3; slot++)
                    if (EquippedItem(gang, slot) is { } itemId)
                        batch.Draw(_itemPortraits, GangInformationLayout.Equipment(slot),
                            OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
            }
            font.Draw(batch, definition.Name,
                new Vector2(definitionOnly ? GangDefinitionInformationLayout.NameLeft : SharedPanelLayout.X(98),
                    definitionOnly ? GangDefinitionInformationLayout.NameY : SharedPanelLayout.Y(28)), Color.Lime, 1);
            var descriptionColumns = definitionOnly ? 30 : 27;
            foreach (var entry in WrapPanelText(definition.Description, descriptionColumns).Take(3)
                         .Select((text, row) => (text, row)))
                font.Draw(batch, entry.text,
                    new Vector2(definitionOnly ? GangDefinitionInformationLayout.DescriptionLeft : SharedPanelLayout.X(98),
                        definitionOnly ? GangDefinitionInformationLayout.DescriptionY(entry.row)
                            : SharedPanelLayout.Y(45 + entry.row * 10)), Color.Lime, 1);
            var forceLeft = definitionOnly ? GangDefinitionInformationLayout.LeftValueLeft
                : GangInformationLayout.LeftValueLeft;
            var forceY = definitionOnly ? GangDefinitionInformationLayout.ForceY : SharedPanelLayout.Y(92);
            if (gang is null)
                DrawGangPanelValue(font, batch, "??", forceLeft, forceY);
            else
                DrawNativeTwoCellValue(font, batch, gang.Force, forceLeft, forceY,
                    NativeTwoCellNumberPresentation.Kind.Baseline);
            DrawNativeTwoCellValue(font, batch, -definition.Upkeep,
                definitionOnly ? GangDefinitionInformationLayout.RightValueLeft : GangInformationLayout.RightValueLeft,
                definitionOnly ? GangDefinitionInformationLayout.ForceY : SharedPanelLayout.Y(92),
                NativeTwoCellNumberPresentation.Kind.Baseline);
            DrawNativeTwoCellValue(font, batch, definition.TechLevel,
                definitionOnly ? GangDefinitionInformationLayout.RightValueLeft : GangInformationLayout.RightValueLeft,
                definitionOnly ? GangDefinitionInformationLayout.TechLevelY : SharedPanelLayout.Y(101),
                NativeTwoCellNumberPresentation.Kind.Baseline);
            int[] left = [stats.Combat, stats.Defense, stats.Chaos, stats.Control, stats.Heal, stats.Influence, stats.Research];
            int[] right = [stats.Stealth, stats.Detect, stats.Strength, stats.Blade, stats.Range, stats.Fighting, stats.MartialArts];
            for (var index = 0; index < left.Length; index++)
            {
                var y = definitionOnly ? GangDefinitionInformationLayout.StatisticY(index)
                    : GangInformationLayout.StatisticY(index);
                DrawNativeTwoCellValue(font, batch, left[index], definitionOnly
                    ? GangDefinitionInformationLayout.LeftValueLeft : GangInformationLayout.LeftValueLeft, y,
                    index < 2 ? NativeTwoCellNumberPresentation.Kind.Baseline
                        : NativeTwoCellNumberPresentation.Kind.Modifier);
                DrawNativeTwoCellValue(font, batch, right[index], definitionOnly
                    ? GangDefinitionInformationLayout.RightValueLeft : GangInformationLayout.RightValueLeft, y,
                    index < 2 ? NativeTwoCellNumberPresentation.Kind.Baseline
                        : NativeTwoCellNumberPresentation.Kind.Modifier);
            }
        }
        if (_hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.GangAt(hover,
                gang is null
                    ? null
                    : effect => GangStatisticModifierTooltip.Lines(effect, state, gang)));
    }

    private static void ClearGangInformationFields(
        SpriteBatch batch, Texture2D pixel, bool definitionOnly)
    {
        var nameLeft = definitionOnly ? GangDefinitionInformationLayout.NameLeft : SharedPanelLayout.X(96);
        var nameY = definitionOnly ? GangDefinitionInformationLayout.NameY : SharedPanelLayout.Y(27);
        var descriptionLeft = definitionOnly ? GangDefinitionInformationLayout.DescriptionLeft : SharedPanelLayout.X(96);
        var descriptionY = definitionOnly ? GangDefinitionInformationLayout.DescriptionY(0) : SharedPanelLayout.Y(44);
        var leftValueLeft = definitionOnly ? GangDefinitionInformationLayout.LeftValueLeft
            : GangInformationLayout.LeftValueLeft;
        var rightValueLeft = definitionOnly ? GangDefinitionInformationLayout.RightValueLeft
            : GangInformationLayout.RightValueLeft;
        var forceY = definitionOnly ? GangDefinitionInformationLayout.ForceY : SharedPanelLayout.Y(92);
        var techLevelY = definitionOnly ? GangDefinitionInformationLayout.TechLevelY : SharedPanelLayout.Y(101);
        batch.Draw(pixel, new Rectangle(nameLeft, nameY, 180, 10), Color.Black);
        batch.Draw(pixel, new Rectangle(descriptionLeft, descriptionY, 186, 37), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(leftValueLeft, forceY), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, forceY), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, techLevelY), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = definitionOnly ? GangDefinitionInformationLayout.StatisticY(row)
                : GangInformationLayout.StatisticY(row);
            batch.Draw(pixel, GangInformationLayout.ValueField(leftValueLeft, y), Color.Black);
            batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, y), Color.Black);
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

    private static short? EquippedItem(MatchGangState gang, int slot) => slot switch
    {
        0 => gang.WeaponItemId,
        1 => gang.ArmorItemId,
        2 => gang.MiscellaneousItemId,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static void DrawPanelValue(PixelFont font, SpriteBatch batch, int value, int right, int y)
        => DrawPanelValue(font, batch, value.ToString(), right, y);

    private static void DrawPanelValue(PixelFont font, SpriteBatch batch, string text, int right, int y)
    {
        DrawPanelValue(font, batch, text, right, y, Color.Lime);
    }

    private static void DrawPanelValue(
        PixelFont font, SpriteBatch batch, string text, int right, int y, Color color) =>
        font.Draw(batch, text, new Vector2(right - text.Length * 6, y), color, 1);

    private static void DrawNativeTwoCellValue(
        PixelFont font, SpriteBatch batch, int value, int left, int y,
        NativeTwoCellNumberPresentation.Kind kind = NativeTwoCellNumberPresentation.Kind.Modifier)
    {
        DrawNativeFixedWidthValue(font, batch, value, left, y, 2, kind);
    }

    private static void DrawNativeFixedWidthValue(
        PixelFont font, SpriteBatch batch, int value, int left, int y, int width,
        NativeTwoCellNumberPresentation.Kind kind = NativeTwoCellNumberPresentation.Kind.Baseline)
    {
        var display = NativeTwoCellNumberPresentation.Format(value, kind, width);
        font.Draw(batch, display.Digits,
            new Vector2(left + (width - display.Digits.Length) * OriginalFontLayout.CellWidth, y),
            display.IsNegative ? Color.Red : display.IsDim ? new Color(0, 137, 0) : Color.Lime, 1);
    }

    private static void DrawGangPanelValue(
        PixelFont font, SpriteBatch batch, string text, int left, int y)
    {
        if (text.Length is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(text), "Gang values use two native glyph cells.");
        font.Draw(batch, text,
            new Vector2(GangInformationLayout.ValueTextLeft(left, text), y), Color.Lime, 1);
    }
}

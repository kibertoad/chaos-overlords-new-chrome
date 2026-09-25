using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// Whether the open gang panel is the compact one an order panel opens (SCR-GANG-001); the
    /// other callers open the Gang Information panel of SCR-GANG-002.
    /// </summary>
    private bool _gangDetailsCompact;

    /// <summary>When the carried items' rotation last started at frame 0 (SCR-GANG-002).</summary>
    private TimeSpan _gangDetailsAnimationStart;

    private Texture2D? _gangBaseValueDimPattern;

    private void OpenSectorGangDetails(ClientScreen returnScreen) =>
        OpenSectorGangs(returnScreen);

    /// <summary>
    /// Opens a gang that exists. An order panel (the Commands screen) opens the compact panel of
    /// SCR-GANG-001 with the gang's record (FND-GANG-009); every other caller opens SCR-GANG-002.
    /// </summary>
    private void OpenGangDetails(
        MatchGangState gang,
        ClientScreen returnScreen,
        int? sectorFilter = null)
    {
        _gangDetailsInstanceId = gang.Id;
        _gangDetailsDefinitionId = gang.DefinitionId;
        _gangDetailsReturnScreen = returnScreen;
        _gangDetailsSectorFilter = sectorFilter;
        _gangDetailsCompact = returnScreen == ClientScreen.Commands;
        _gangDetailsAnimationStart = _inputTime;
        _gangEquipmentItemClicks.Cancel();
        _screens.Show(ClientScreen.Gang);
    }

    /// <summary>
    /// Opens a hire offer on the Gang Information panel of SCR-GANG-002, as a Force 0 record with
    /// no items, so Force shows two question marks (FND-HIRE-008, FND-GANG-008).
    /// </summary>
    private void OpenGangDefinitionDetails(short definitionId, ClientScreen returnScreen)
    {
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = definitionId;
        _gangDetailsReturnScreen = returnScreen;
        _gangDetailsSectorFilter = null;
        _gangDetailsCompact = false;
        _gangDetailsAnimationStart = _inputTime;
        _gangEquipmentItemClicks.Cancel();
        _screens.Show(ClientScreen.Gang);
    }

    private void CloseGangDetails()
    {
        var returnScreen = _gangDetailsReturnScreen;
        _gangDetailsInstanceId = null;
        _gangDetailsDefinitionId = null;
        _gangDetailsSectorFilter = null;
        _gangDetailsCompact = false;
        _gangEquipmentItemClicks.Cancel();
        _screens.Show(returnScreen);
    }

    private void CycleGangDetails(int delta)
    {
        // Opened from a command overlay, the panel describes the gang being ordered and
        // must hand the overlay back that same gang.
        if (_gangDetailsReturnScreen is ClientScreen.Commands or ClientScreen.Give or ClientScreen.Sell)
            return;
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
        _gangDetailsAnimationStart = _inputTime;
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

    /// <summary>The panel the open gang panel covers, for the outside-the-panel test.</summary>
    private Rectangle GangDetailsPanel => _gangDetailsCompact
        ? GangDefinitionInformationLayout.Panel
        : GangInformationLayout.Panel;

    private Rectangle GangDetailsOk => _gangDetailsCompact
        ? GangDefinitionInformationLayout.Ok
        : GangInformationLayout.Ok;

    private bool HandleGangDetailsEquipmentClick(Point point)
    {
        // SCR-GANG-001 draws no items; only SCR-GANG-002 opens an item from its picture.
        if (_gangDetailsCompact
            || _gangDetailsInstanceId is not { } gangId
            || _state?.FindGang(gangId) is not { } gang
            || GangInformationLayout.EquipmentSlotAt(point) is not { } slot)
            return false;

        var itemId = EquippedItem(gang, slot);
        if (itemId is { } resolved
            && _gangEquipmentItemClicks.Register(slot, _inputTime))
        {
            // FND-GANG-006: the rotation restarts at frame 0 after the item's panel.
            _gangDetailsAnimationStart = _inputTime;
            OpenItemDetails(resolved, ClientScreen.Gang);
        }
        return true;
    }

    private void DrawGangDetailsBackdrop(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_gangDetailsReturnScreen == ClientScreen.Commands)
            DrawCommands(batch, pixel, font, state);
        else if (_gangDetailsReturnScreen == ClientScreen.Give)
            DrawGiveEquipment(batch, pixel, font, state);
        else if (_gangDetailsReturnScreen == ClientScreen.Sell)
            DrawSellEquipment(batch, pixel, font, state);
        else
            DrawMapBackdrop(batch, pixel, font, state, _gangDetailsReturnScreen);
    }

    /// <summary>
    /// Draws SCR-GANG-002 (PX05000, the shared panel) or, from an order panel, SCR-GANG-001
    /// (PX05022's 320-pixel crop). Both draw the gang's own values; with Base Statistics on they
    /// also draw the definition's values 18 pixels to the left, dimmed on the compact panel.
    /// </summary>
    private void DrawGangDetailsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        var compact = _gangDetailsCompact;
        if (compact)
        {
            if (_gangDefinitionInfoBackground is not null)
                batch.Draw(_gangDefinitionInfoBackground, GangDefinitionInformationLayout.Panel,
                    GangDefinitionInformationLayout.BackgroundSource, Color.White);
            else
                batch.Draw(pixel, GangDefinitionInformationLayout.Panel, new Color(0, 0, 0, 245));
        }
        else
            DrawPanelArtwork(batch, pixel, _gangInfoBackground, GangInformationLayout.Panel);

        var nameLeft = compact ? GangDefinitionInformationLayout.NameLeft : GangInformationLayout.NameLeft;
        var nameY = compact ? GangDefinitionInformationLayout.NameY : GangInformationLayout.NameY;
        var gang = _gangDetailsInstanceId is { } instanceId ? state.FindGang(instanceId) : null;
        var definitionId = gang?.DefinitionId ?? _gangDetailsDefinitionId;
        if (definitionId is null)
        {
            font.Draw(batch, "NO ACTIVE GANG", new Vector2(nameLeft, nameY), Color.White, 1);
            return;
        }

        var definition = state.Definitions.Gang(definitionId.Value);
        var baseStats = EffectiveStatistics.From(definition.Stats);
        // FND-GANG-008: a hire offer is shown as a record with no items in no sector, so its
        // values are the definition's with the bare-handed weapon skills in Combat (RULE-COMBAT-001).
        var stats = gang is null
            ? baseStats with
            {
                Combat = checked(baseStats.Combat + ManualRules.WeaponSkills(baseStats, null))
            }
            : EffectiveStatisticsCalculator.ForGang(state, gang);
        ClearGangInformationFields(batch, pixel, compact);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, compact
                    ? GangDefinitionInformationLayout.Portrait
                    : GangInformationLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        if (!compact && gang is not null)
            DrawGangDetailsEquipment(batch, gang);
        font.Draw(batch, definition.Name, new Vector2(nameLeft, nameY), Color.Lime, 1);
        // FMT-DATA-002: the description is drawn as three fixed rows of 30 characters.
        foreach (var entry in ItemInformationLayout.DescriptionLines(definition.Description)
                     .Select((text, row) => (text, row)))
            if (entry.text is not null)
                font.Draw(batch, entry.text, new Vector2(nameLeft, compact
                    ? GangDefinitionInformationLayout.DescriptionY(entry.row)
                    : GangInformationLayout.DescriptionY(entry.row)), Color.Lime, 1);

        var leftValueLeft = compact
            ? GangDefinitionInformationLayout.LeftValueLeft : GangInformationLayout.LeftValueLeft;
        var rightValueLeft = compact
            ? GangDefinitionInformationLayout.RightValueLeft : GangInformationLayout.RightValueLeft;
        var forceY = compact ? GangDefinitionInformationLayout.ForceY : GangInformationLayout.ForceY;
        var techLevelY = compact
            ? GangDefinitionInformationLayout.TechLevelY : GangInformationLayout.TechLevelY;
        // SCR-GANG-001, SCR-GANG-002: a record whose Force is 0 shows two question marks.
        var force = gang?.Force ?? 0;
        if (force == 0)
            DrawGangPanelValue(font, batch, "??", leftValueLeft, forceY);
        else
            DrawNativeTwoCellValue(font, batch, force, leftValueLeft, forceY,
                NativeTwoCellNumberPresentation.Kind.Baseline);
        DrawNativeTwoCellValue(font, batch, -definition.Upkeep, rightValueLeft, forceY,
            NativeTwoCellNumberPresentation.Kind.Baseline);
        DrawNativeTwoCellValue(font, batch, definition.TechLevel, rightValueLeft, techLevelY,
            NativeTwoCellNumberPresentation.Kind.Baseline);
        DrawGangStatisticColumns(font, batch, stats, leftValueLeft, rightValueLeft, compact,
            baseValues: false);
        if (_showBaseStatistics)
        {
            DrawGangStatisticColumns(font, batch, baseStats,
                leftValueLeft - GangInformationLayout.BaseValueOffset,
                rightValueLeft - GangInformationLayout.BaseValueOffset, compact, baseValues: true);
            if (compact) DrawGangBaseValueDimming(batch);
        }

        // DEV-GANG-001: the full panel's statistics explain themselves on hover.
        if (!compact && _hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover, InformationEffectTooltips.GangAt(hover,
                gang is null
                    ? null
                    : effect => GangStatisticModifierTooltip.Lines(effect, state, gang)));
    }

    /// <summary>
    /// Combat, Defense, Stealth and Detect as plain numbers and the ten skills as modifiers
    /// (FND-GANG-006, FND-GANG-010, RULE-UI-004). The base values all go through the plain
    /// number helper.
    /// </summary>
    private static void DrawGangStatisticColumns(
        PixelFont font, SpriteBatch batch, EffectiveStatistics stats, int leftValueLeft,
        int rightValueLeft, bool compact, bool baseValues)
    {
        int[] left = [stats.Combat, stats.Defense, stats.Chaos, stats.Control, stats.Heal, stats.Influence, stats.Research];
        int[] right = [stats.Stealth, stats.Detect, stats.Strength, stats.Blade, stats.Range, stats.Fighting, stats.MartialArts];
        for (var index = 0; index < left.Length; index++)
        {
            var y = compact ? GangDefinitionInformationLayout.StatisticY(index)
                : GangInformationLayout.StatisticY(index);
            var kind = baseValues || index < 2
                ? NativeTwoCellNumberPresentation.Kind.Baseline
                : NativeTwoCellNumberPresentation.Kind.Modifier;
            DrawNativeTwoCellValue(font, batch, left[index], leftValueLeft, y, kind);
            DrawNativeTwoCellValue(font, batch, right[index], rightValueLeft, y, kind);
        }
    }

    /// <summary>
    /// SCR-GANG-001, FND-GANG-010, FND-GANG-011: black drawn through bitmap 143, the pattern
    /// 0x7FFF selects, over the base values. Each area starts the pattern at its own corner, so
    /// its top-left pixel is black.
    /// </summary>
    private void DrawGangBaseValueDimming(SpriteBatch batch)
    {
        _gangBaseValueDimPattern ??= CreateBaseValueDimPattern(GraphicsDevice);
        foreach (var area in GangDefinitionInformationLayout.BaseValueDimAreas)
            batch.Draw(_gangBaseValueDimPattern, area,
                new Rectangle(0, 0, area.Width, area.Height), Color.White);
    }

    private static Texture2D CreateBaseValueDimPattern(GraphicsDevice graphicsDevice)
    {
        const int size = 64;
        var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(OriginalPatternMask.ShadedRectangle(
            GangDefinitionInformationLayout.BaseValueDimPattern, size, size, Color.Black, Color.Black));
        return texture;
    }

    /// <summary>
    /// SCR-GANG-002, FND-GANG-006: each carried item's rotation strip, 15 frames of 48 by 48,
    /// starting at frame 0 when the panel opens.
    /// </summary>
    private void DrawGangDetailsEquipment(SpriteBatch batch, MatchGangState gang)
    {
        var elapsed = _inputTime - _gangDetailsAnimationStart;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var frame = ItemRotationPresentation.Frame(elapsed);
        for (var slot = 0; slot < 3; slot++)
        {
            if (EquippedItem(gang, slot) is not { } itemId) continue;
            if (itemId >= 0 && itemId < _itemRotationTextures.Length
                && _itemRotationTextures[itemId] is { } rotation)
                batch.Draw(rotation, GangInformationLayout.Equipment(slot), frame, Color.White);
            else if (_itemPortraits is not null)
                batch.Draw(_itemPortraits, GangInformationLayout.Equipment(slot),
                    OriginalSpriteLayout.ItemPortrait(itemId), Color.White);
        }
    }

    private static void ClearGangInformationFields(
        SpriteBatch batch, Texture2D pixel, bool compact)
    {
        var nameLeft = compact ? GangDefinitionInformationLayout.NameLeft : GangInformationLayout.NameLeft;
        var nameY = compact ? GangDefinitionInformationLayout.NameY : GangInformationLayout.NameY;
        var descriptionY = compact
            ? GangDefinitionInformationLayout.DescriptionY(0) : GangInformationLayout.DescriptionY(0);
        var leftValueLeft = compact ? GangDefinitionInformationLayout.LeftValueLeft
            : GangInformationLayout.LeftValueLeft;
        var rightValueLeft = compact ? GangDefinitionInformationLayout.RightValueLeft
            : GangInformationLayout.RightValueLeft;
        var forceY = compact ? GangDefinitionInformationLayout.ForceY : GangInformationLayout.ForceY;
        var techLevelY = compact
            ? GangDefinitionInformationLayout.TechLevelY : GangInformationLayout.TechLevelY;
        var textWidth = ItemInformationLayout.DescriptionColumns * OriginalFontLayout.CellWidth;
        batch.Draw(pixel, new Rectangle(nameLeft, nameY, textWidth, OriginalFontLayout.GlyphHeight),
            Color.Black);
        batch.Draw(pixel, new Rectangle(nameLeft, descriptionY, textWidth,
            2 * OriginalFontLayout.LineHeight + OriginalFontLayout.GlyphHeight), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(leftValueLeft, forceY), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, forceY), Color.Black);
        batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, techLevelY), Color.Black);
        for (var row = 0; row < 7; row++)
        {
            var y = compact ? GangDefinitionInformationLayout.StatisticY(row)
                : GangInformationLayout.StatisticY(row);
            batch.Draw(pixel, GangInformationLayout.ValueField(leftValueLeft, y), Color.Black);
            batch.Draw(pixel, GangInformationLayout.ValueField(rightValueLeft, y), Color.Black);
        }
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

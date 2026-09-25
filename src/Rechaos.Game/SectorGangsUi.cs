using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>The Gangs in Sector panel (SCR-UI-005, FND-UI-014).</summary>
public static class SectorGangsLayout
{
    public const int MaximumGangCount = 6;
    public static Rectangle Panel => SharedPanelLayout.Panel;

    /// <summary>SCR-UI-005, FND-UI-014: the close face, panel-local (33,169)-(82,191).</summary>
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);

    /// <summary>SCR-UI-005, FND-UI-014: the sector's 54-by-52 map cell, framed in black.</summary>
    public static Rectangle SectorTile => new(135, 135, CityMapLayout.TileWidth, CityMapLayout.TileHeight);

    /// <summary>SCR-UI-005, FND-UI-014: where the column letter and row digit start.</summary>
    public static Point SectorCode => new(156, 190);

    /// <summary>SCR-UI-005, FND-UI-014: the column letter A to H and the row digit 1 to 8.</summary>
    public static string SectorCodeText(int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return $"{(char)('A' + sectorId % MatchLimits.BoardWidth)}{(char)('1' + sectorId / MatchLimits.BoardWidth)}";
    }

    /// <summary>SCR-UI-005, FND-UI-014: the scaled portrait of column n at (248 + 32n, 138).</summary>
    public static Rectangle GangCard(int index)
    {
        if (index is < 0 or >= MaximumGangCount) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(SharedPanelLayout.X(144 + index * 32),
            SharedPanelLayout.Y(14), 32, 32);
    }

    /// <summary>The left edge of the native two-cell statistic field.</summary>
    public static int ValueLeft(int index)
    {
        if (index is < 0 or >= MaximumGangCount) throw new ArgumentOutOfRangeException(nameof(index));
        return SharedPanelLayout.X(154 + index * 32);
    }

    public static int ValueY(int row)
    {
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            // These are the PX05009 label baselines at panel-local y=48..186.
            // They deliberately do not share the card's global coordinate system.
            0 => SharedPanelLayout.Y(48),
            1 => SharedPanelLayout.Y(57),
            2 => SharedPanelLayout.Y(67),
            3 => SharedPanelLayout.Y(76),
            4 => SharedPanelLayout.Y(85),
            5 => SharedPanelLayout.Y(94),
            6 => SharedPanelLayout.Y(104),
            7 => SharedPanelLayout.Y(113),
            8 => SharedPanelLayout.Y(122),
            9 => SharedPanelLayout.Y(131),
            10 => SharedPanelLayout.Y(140),
            11 => SharedPanelLayout.Y(150),
            12 => SharedPanelLayout.Y(159),
            13 => SharedPanelLayout.Y(168),
            14 => SharedPanelLayout.Y(177),
            15 => SharedPanelLayout.Y(186),
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
}

public sealed partial class ChaosGame
{
    /// <summary>The sector the Gangs in Sector panel was opened for.</summary>
    private int _sectorGangSector;

    private void OpenSectorGangs(ClientScreen returnScreen)
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId) return;
        var gangs = GangInformationRoster.ForSector(_state.FindPlayer(playerId)!.Gangs, _cursor);
        if (gangs.Count == 0)
        {
            RejectInput("NO GANGS IN SELECTED SECTOR");
            return;
        }
        _sectorGangRoster = gangs.Select(gang => gang.Id).ToArray();
        _sectorGangSector = _cursor;
        _sectorGangCursor = 0;
        _sectorGangReturnScreen = returnScreen;
        SelectSectorGang();
        _screens.Show(ClientScreen.SectorGangs);
    }

    private void MoveSectorGangCursor(int delta)
    {
        if (_sectorGangRoster.Count == 0) return;
        _sectorGangCursor = Mod(_sectorGangCursor + delta, _sectorGangRoster.Count);
        SelectSectorGang();
    }

    private void SelectSectorGang()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId || _sectorGangRoster.Count == 0) return;
        var gangId = _sectorGangRoster[_sectorGangCursor];
        var active = _state.FindPlayer(playerId)!.Gangs.Where(gang => gang.IsActive).ToArray();
        var index = Array.FindIndex(active, gang => gang.Id == gangId);
        if (index >= 0) _selectedGangIndex = index;
    }

    private void CloseSectorGangs() => _screens.Show(_sectorGangReturnScreen);

    private void DrawSectorGangs(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawMapBackdrop(batch, pixel, font, state, _sectorGangReturnScreen);
        DrawSectorGangsPanel(batch, pixel, font, state);
    }

    private void DrawSectorGangsPanel(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _sectorGangsBackground, SectorGangsLayout.Panel);
        DrawCitySectorCell(batch, pixel, state, _sectorGangSector, SectorGangsLayout.SectorTile.Location);
        DrawBorder(batch, pixel, SectorGangsLayout.SectorTile, Color.Black, 1);
        font.Draw(batch, SectorGangsLayout.SectorCodeText(_sectorGangSector),
            SectorGangsLayout.SectorCode.ToVector2(), Color.Lime, 1);
        foreach (var entry in _sectorGangRoster.Take(SectorGangsLayout.MaximumGangCount)
                     .Select((id, index) => (id, index)))
        {
            var gang = state.FindGang(entry.id);
            if (gang is null) continue;
            var definition = state.Definitions.Gang(gang.DefinitionId);
            // SCR-UI-005, FND-UI-014: the panel never reads the Base Statistics option.
            var stats = EffectiveStatisticsCalculator.ForGang(state, gang);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, SectorGangsLayout.GangCard(entry.index),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            // SCR-UI-005: Upkeep is drawn negated, so it shows in red.
            int[] values =
            [
                definition.TechLevel, -definition.Upkeep, stats.Combat, stats.Defense,
                stats.Stealth, stats.Detect, stats.Chaos, stats.Control, stats.Heal,
                stats.Influence, stats.Research, stats.Strength, stats.Blade,
                stats.Range, stats.Fighting, stats.MartialArts
            ];
            for (var row = 0; row < values.Length; row++)
            {
                var y = SectorGangsLayout.ValueY(row);
                var valueLeft = SectorGangsLayout.ValueLeft(entry.index);
                batch.Draw(pixel, new Rectangle(valueLeft, y, 12, 7), Color.Black);
                DrawNativeTwoCellValue(font, batch, values[row], valueLeft, y,
                    row < 6 ? NativeTwoCellNumberPresentation.Kind.Baseline
                        : NativeTwoCellNumberPresentation.Kind.Modifier);
            }
        }
    }
}

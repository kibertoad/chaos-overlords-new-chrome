using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SectorGangsLayout
{
    public const int MaximumGangCount = 6;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle GangCard(int index)
    {
        if (index is < 0 or >= MaximumGangCount) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(144 + index * 32, 158, 32, 32);
    }

    public static int ValueRight(int index)
    {
        if (index is < 0 or >= MaximumGangCount) throw new ArgumentOutOfRangeException(nameof(index));
        return 154 + index * 32;
    }

    public static int ValueY(int row)
    {
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => 192,
            1 => 201,
            2 => 211,
            3 => 220,
            4 => 229,
            5 => 238,
            6 => 248,
            7 => 257,
            8 => 266,
            9 => 275,
            10 => 284,
            11 => 294,
            12 => 303,
            13 => 312,
            14 => 321,
            15 => 330,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
}

public sealed partial class ChaosGame
{
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
        if (_sectorGangReturnScreen == ClientScreen.Sector) DrawSectorDetails(batch, pixel, font, state);
        else DrawBoard(batch, pixel, font, state);
        if (_sectorGangsBackground is not null)
            batch.Draw(_sectorGangsBackground, SectorGangsLayout.Panel, Color.White);
        else
            batch.Draw(pixel, SectorGangsLayout.Panel, new Color(0, 0, 0, 245));
        foreach (var entry in _sectorGangRoster.Take(SectorGangsLayout.MaximumGangCount)
                     .Select((id, index) => (id, index)))
        {
            var gang = state.FindGang(entry.id);
            if (gang is null) continue;
            var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
            var stats = _showBaseStatistics
                ? EffectiveStatistics.From(definition.Stats)
                : EffectiveStatisticsCalculator.ForGang(state, gang);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, SectorGangsLayout.GangCard(entry.index),
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            int[] values =
            [
                definition.TechLevel, definition.Upkeep, stats.Combat, stats.Defense,
                stats.Stealth, stats.Detect, stats.Chaos, stats.Control, stats.Heal,
                stats.Influence, stats.Research, stats.Strength, stats.Blade,
                stats.Range, stats.Fighting, stats.MartialArts
            ];
            for (var row = 0; row < values.Length; row++)
            {
                var y = SectorGangsLayout.ValueY(row);
                var valueRight = SectorGangsLayout.ValueRight(entry.index);
                batch.Draw(pixel, new Rectangle(valueRight - 12, y, 12, 7), Color.Black);
                DrawPanelValue(font, batch, values[row], valueRight, y);
            }
        }
    }
}

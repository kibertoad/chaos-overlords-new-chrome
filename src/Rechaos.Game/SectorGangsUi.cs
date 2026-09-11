using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SectorGangsLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => new(134, 135, 55, 65);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public const int ValueRight = 432;

    public static int ValueY(int row)
    {
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => 173,
            1 => 182,
            2 => 192,
            3 => 201,
            4 => 210,
            5 => 219,
            6 => 229,
            7 => 238,
            8 => 247,
            9 => 256,
            10 => 265,
            11 => 275,
            12 => 284,
            13 => 293,
            14 => 302,
            15 => 311,
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
            _message = "NO GANGS IN SELECTED SECTOR";
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
        if (_sectorGangRoster.Count == 0) return;
        var gang = state.FindGang(_sectorGangRoster[_sectorGangCursor]);
        if (gang is null) return;
        var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
        var stats = _showBaseStatistics
            ? EffectiveStatistics.From(definition.Stats)
            : EffectiveStatisticsCalculator.ForGang(state, gang);
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, SectorGangsLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
        batch.Draw(pixel, new Rectangle(200, 137, 232, 9), Color.Black);
        font.Draw(batch, definition.Name, new Vector2(202, 138), Color.Lime, 1);
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
            batch.Draw(pixel, new Rectangle(414, y, 18, 7), Color.Black);
            DrawPanelValue(font, batch, values[row], SectorGangsLayout.ValueRight, y);
        }
    }
}

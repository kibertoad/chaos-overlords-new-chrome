using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum ClientScreen
{
    Title,
    Setup,
    City,
    Commands,
    Hire,
    Events,
    Sector,
    Gang,
    Finance,
    Ranking,
    Items,
    Give,
    CombatSummary,
    Search,
    Handoff,
    Endgame
}

public sealed class ScreenRouter
{
    public ClientScreen Current { get; private set; } = ClientScreen.Title;

    public void Show(ClientScreen screen) => Current = screen;

    public bool Back()
    {
        if (Current == ClientScreen.Title) return false;
        Current = Current is ClientScreen.Events or ClientScreen.Commands or ClientScreen.Hire
            or ClientScreen.Sector or ClientScreen.Gang or ClientScreen.Finance or ClientScreen.Ranking
            or ClientScreen.Items or ClientScreen.Give
            or ClientScreen.CombatSummary
            or ClientScreen.Search
            ? Current == ClientScreen.Give ? ClientScreen.Items : ClientScreen.City
            : ClientScreen.Title;
        return true;
    }
}

public static class VirtualInput
{
    public const int Width = 640;
    public const int Height = 460;

    public static Matrix Transform(Viewport viewport)
    {
        var scale = MathF.Min(viewport.Width / (float)Width, viewport.Height / (float)Height);
        return Matrix.CreateScale(scale) * Matrix.CreateTranslation(
            (viewport.Width - Width * scale) / 2,
            (viewport.Height - Height * scale) / 2,
            0);
    }

    public static bool TryMap(Viewport viewport, Point physical, out Point virtualPoint)
    {
        var scale = MathF.Min(viewport.Width / (float)Width, viewport.Height / (float)Height);
        var left = (viewport.Width - Width * scale) / 2;
        var top = (viewport.Height - Height * scale) / 2;
        var x = (physical.X - left) / scale;
        var y = (physical.Y - top) / scale;
        virtualPoint = new Point((int)x, (int)y);
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }
}

public sealed class CitySectorClickTracker
{
    public static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(500);
    private int? _lastSector;
    private TimeSpan _lastClick;

    public bool Register(int sectorId, TimeSpan timestamp)
    {
        _ = CityMapLayout.Source(sectorId);
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        var doubleClick = _lastSector == sectorId
            && timestamp >= _lastClick
            && timestamp - _lastClick <= DoubleClickWindow;
        _lastSector = doubleClick ? null : sectorId;
        _lastClick = timestamp;
        return doubleClick;
    }

    public void Cancel() => _lastSector = null;
}

public sealed class IndexedDoubleClickTracker
{
    private int? _lastIndex;
    private TimeSpan _lastClick;

    public bool Register(int index, TimeSpan timestamp)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        var doubleClick = _lastIndex == index
            && timestamp >= _lastClick
            && timestamp - _lastClick <= CitySectorClickTracker.DoubleClickWindow;
        _lastIndex = doubleClick ? null : index;
        _lastClick = timestamp;
        return doubleClick;
    }

    public void Cancel() => _lastIndex = null;
}

/// <summary>Native layout of the 8x8 sector cells in the PX10000-PX10006 city layers.</summary>
public static class CityMapLayout
{
    public const int Left = 2;
    public const int Top = 44;
    public const int TileWidth = 54;
    public const int TileHeight = 52;

    public static Rectangle Source(int sectorId)
    {
        ValidateSector(sectorId);
        return new Rectangle(
            sectorId % 8 * TileWidth,
            sectorId / 8 * TileHeight,
            TileWidth,
            TileHeight);
    }

    public static Rectangle Destination(int sectorId)
    {
        var source = Source(sectorId);
        return new Rectangle(Left + source.X, Top + source.Y, source.Width, source.Height);
    }

    public static int OwnershipSheet(PlayerId? owner) => owner?.Value + 1 ?? 0;

    public static bool TrySectorAt(Point point, out int sectorId)
    {
        var x = point.X - Left;
        var y = point.Y - Top;
        if (x < 0 || x >= TileWidth * 8 || y < 0 || y >= TileHeight * 8)
        {
            sectorId = -1;
            return false;
        }
        sectorId = y / TileHeight * 8 + x / TileWidth;
        return true;
    }

    private static void ValidateSector(int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
    }
}

public static class OriginalSpriteLayout
{
    public static Rectangle PolicePatrolCar => new(116, 0, 48, 64);
    public static Rectangle HiredStamp => new(120, 300, 60, 60);
    public static Rectangle AssignedGangStatus => new(492, 67, 20, 20);
    public static Rectangle IdleGangStatus => new(492, 107, 20, 20);
    public static Rectangle IncomingGangStatus => new(492, 147, 20, 20);
    public static Rectangle GangCardFrame => new(164, 17, 70, 118);
    public static Rectangle SectorBackArrow => new(120, 211, 30, 47);

    public static Rectangle OverlordPortrait(int portraitId)
    {
        if (portraitId is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(portraitId));
        return new Rectangle(portraitId * 32, 480, 32, 32);
    }

    public static Rectangle SitePortrait(int definitionId)
    {
        if (definitionId is < 0 or >= 22) throw new ArgumentOutOfRangeException(nameof(definitionId));
        return new Rectangle(0, definitionId * 64, 120, 64);
    }

    public static Rectangle GangPortrait(int definitionId)
    {
        if (definitionId is < 0 or >= 90) throw new ArgumentOutOfRangeException(nameof(definitionId));
        return new Rectangle(definitionId % 10 * 64, definitionId / 10 * 64, 64, 64);
    }
}

public static class GangStatusMarkerLayout
{
    public static Rectangle Destination(int sectorId)
    {
        var sector = CityMapLayout.Destination(sectorId);
        return new Rectangle(sector.Right - 22, sector.Y + 20, 20, 20);
    }
}

public static class SectorDetailLayout
{
    public const int Left = 61;
    public const int Top = 48;
    public const int Columns = 3;
    public const int Rows = 3;
    public static Rectangle Back => new(4, 394, 28, 66);

    public static Rectangle Cell(int column, int row)
    {
        if (column is < 0 or >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
        if (row is < 0 or >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(
            Left + column * CityMapLayout.TileWidth,
            Top + row * CityMapLayout.TileHeight,
            CityMapLayout.TileWidth,
            CityMapLayout.TileHeight);
    }

    public static int? SectorAt(int centerSectorId, int column, int row)
    {
        _ = CityMapLayout.Source(centerSectorId);
        _ = Cell(column, row);
        var centerColumn = centerSectorId % 8;
        var centerRow = centerSectorId / 8;
        var sectorColumn = centerColumn + column - 1;
        var sectorRow = centerRow + row - 1;
        return sectorColumn is < 0 or >= 8 || sectorRow is < 0 or >= 8
            ? null
            : sectorRow * 8 + sectorColumn;
    }

    public static bool TrySectorAt(Point point, int centerSectorId, out int sectorId)
    {
        var column = (point.X - Left) / CityMapLayout.TileWidth;
        var row = (point.Y - Top) / CityMapLayout.TileHeight;
        if (point.X < Left || point.Y < Top || column is < 0 or >= Columns || row is < 0 or >= Rows
            || SectorAt(centerSectorId, column, row) is not { } mapped)
        {
            sectorId = -1;
            return false;
        }
        sectorId = mapped;
        return true;
    }

    public static Rectangle? Marker(int centerSectorId, int sectorId)
    {
        _ = CityMapLayout.Source(centerSectorId);
        _ = CityMapLayout.Source(sectorId);
        var deltaColumn = sectorId % 8 - centerSectorId % 8;
        var deltaRow = sectorId / 8 - centerSectorId / 8;
        if (deltaColumn is < -1 or > 1 || deltaRow is < -1 or > 1) return null;
        var cell = Cell(deltaColumn + 1, deltaRow + 1);
        return new Rectangle(cell.Right - 22, cell.Y + 20, 20, 20);
    }

    public static Rectangle SitePortrait(int slot)
    {
        if (slot is < 0 or >= MatchLimits.SitesPerSector)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(83, 226 + slot * 66, 120, 64);
    }

    public static Rectangle SiteControlBar(int slot)
    {
        var portrait = SitePortrait(slot);
        return new Rectangle(portrait.X + 2, portrait.Bottom - 5, portrait.Width - 4, 4);
    }
}

public static class SectorGangCardLayout
{
    public const int VisibleCards = 2;
    public const int Left = 251;
    public const int Top = 80;
    public const int Stride = 74;

    public static Rectangle Frame(int slot) => At(slot, 0, 0, 70, 118);
    public static Rectangle OneOffAction(int slot) => At(slot, 3, 3, 30, 24);
    public static Rectangle RepeatingAction(int slot) => At(slot, 36, 3, 30, 24);
    public static Rectangle Portrait(int slot) => At(slot, 3, 28, 64, 64);
    public static Rectangle ItemSlot(int slot, int itemSlot)
    {
        if (itemSlot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(itemSlot));
        return At(slot, 3 + itemSlot * 21, 93, 21, 22);
    }

    private static Rectangle At(int slot, int x, int y, int width, int height)
    {
        if (slot is < 0 or >= VisibleCards) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(Left + slot * Stride + x, Top + y, width, height);
    }
}

public static class CommandOverlayLayout
{
    public static readonly IReadOnlyList<GangAction> Actions =
    [
        GangAction.Attack, GangAction.Bribe, GangAction.Chaos, GangAction.Control,
        GangAction.Equip, GangAction.Give, GangAction.Heal, GangAction.Hide,
        GangAction.Influence, GangAction.Move, GangAction.Research, GangAction.Sell,
        GangAction.Snitch, GangAction.None, GangAction.Terminate
    ];

    public static Rectangle Panel => new(248, 50, 174, 400);
    public static Rectangle TargetPanel => new(218, 70, 214, 320);
    public static Rectangle ActionRow(int index)
    {
        if (index < 0 || index >= Actions.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(256, 61 + index * 24, 158, 22);
    }

    public static Rectangle TargetRow(int index)
    {
        if (index is < 0 or >= 13) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(226, 100 + index * 20, 198, 18);
    }

    public static bool OpensTargetPicker(GangAction action) => action is
        GangAction.Attack or GangAction.Equip or GangAction.Give or GangAction.Influence
        or GangAction.Move or GangAction.Research or GangAction.Sell;
}

public static class EquipmentCommandLayout
{
    public static Rectangle Panel => new(104, 125, 344, 209);
    public static Rectangle Portrait => new(130, 143, 64, 64);
    public static Rectangle Cancel => new(136, 262, 49, 24);
    public static Rectangle Ok => new(136, 294, 49, 24);
    public static Rectangle ItemRow(int row)
    {
        if (row is < 0 or >= 12) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(248, 154 + row * 12, 184, 11);
    }
}

public static class GangInformationLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => new(130, 143, 64, 62);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public const int LeftValueRight = 287;
    public const int RightValueRight = 383;

    public static int StatisticY(int row) => row switch
    {
        0 => 244,
        1 => 253,
        2 => 271,
        3 => 280,
        4 => 289,
        5 => 298,
        6 => 307,
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };
}

public static class StatusConsoleLayout
{
    public const int LabelLeft = 480;
    public const int ValueRight = 579;
    public const int ScenarioY = 3;
    public const int DateY = 15;
    public const int ScoreY = 24;
    public const int CashY = 42;

    public static int SectorValueY(int row)
    {
        if (row is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(row));
        return 60 + row * 9;
    }
}

public static class DifficultyPresentation
{
    public static string Label(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Goon => "GOON",
        AiDifficulty.Criminal => "CRIMINAL",
        AiDifficulty.CrimeLord => "CRIME LORD",
        AiDifficulty.HomicidalManiac => "HOMICIDAL",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static IReadOnlyList<string> Tooltip(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Goon => ["GOON - EASIEST", "PLAYS TO WIN; LEAST AGGRESSIVE.", "AI PLAYS FAIR: NO BONUSES."],
        AiDifficulty.Criminal => ["CRIMINAL - NORMAL", "PLAYS TO WIN; BALANCED AGGRESSION.", "AI PLAYS FAIR: NO BONUSES."],
        AiDifficulty.CrimeLord => ["CRIME LORD - VERY HARD", "PLAYS TO WIN; MORE AGGRESSIVE.", "AI PLAYS FAIR: NO BONUSES."],
        AiDifficulty.HomicidalManiac => ["HOMICIDAL MANIAC", "TRIES TO STOP YOU WINNING,", "USUALLY BY KILLING YOU.", "AI PLAYS FAIR: NO BONUSES."],
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };
}

public static class PlayerPortraitLayout
{
    public const int Count = 16;

    public static Rectangle SetupTop(int player)
    {
        Validate(player);
        return new Rectangle(360 + player * 36, 32, 32, 32);
    }

    public static Rectangle CityTop(int player)
    {
        Validate(player);
        return new Rectangle(8 + player * 72, 4, 32, 32);
    }
    public static Rectangle SetupLarge(int player) => Player(player, 379, 83, 106, 64, 64, rowStride: 92);
    public static Rectangle Previous(int player) => Player(player, 363, 106, 106, 12, 18, rowStride: 92);
    public static Rectangle Next(int player) => Player(player, 447, 106, 106, 12, 18, rowStride: 92);

    private static Rectangle Player(
        int player,
        int left,
        int top,
        int columnStride,
        int width,
        int height,
        int rowStride = 0)
    {
        if (player is < 0 or >= MatchLimits.PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
        return new Rectangle(left + player % 2 * columnStride, top + player / 2 * rowStride, width, height);
    }

    private static void Validate(int player)
    {
        if (player is < 0 or >= MatchLimits.PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
    }
}

public static class SectorGangView
{
    public const int MaximumPortraits = 10;
    public const int MaximumSearchRows = 7;

    public static IReadOnlyList<MatchGangState> Visible(
        MatchState state,
        PlayerId viewer,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindPlayer(viewer) is null) throw new ArgumentOutOfRangeException(nameof(viewer));
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return state.Players.SelectMany(player => player.Gangs)
            .Where(gang => gang.IsActive && gang.SectorId == sectorId
                && (gang.Owner == viewer || state.CanPlayerDetectGang(viewer, gang.Id)))
            .OrderBy(gang => gang.Owner.Value)
            .ThenBy(gang => gang.Id.Value)
            .ToArray();
    }

    public static Rectangle Portrait(int index)
    {
        if (index is < 0 or >= MaximumPortraits) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(18 + index * 40, 370, 36, 36);
    }

    public static Rectangle SearchPortrait(int index)
    {
        if (index is < 0 or >= MaximumSearchRows) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(18, 107 + index * 40, 36, 36);
    }
}

public static class GangArtLayout
{
    public static Rectangle DetailPortrait => new(67, 90, 64, 64);
    public static Rectangle SelectedEquipmentPortrait => new(558, 58, 56, 56);

    public static Rectangle CombatPortrait(int row, bool defender)
    {
        if (row is < 0 or >= 12) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(defender ? 42 : 18, 108 + row * 24, 20, 20);
    }
}

public sealed record HireDockEntry(short GangDefinitionId, bool Hired);

public static class HireDockLayout
{
    public const int SlotCount = 3;

    public static Rectangle Cell(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(438 + slot * 66, 370, 66, 90);
    }

    public static Rectangle Portrait(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(439 + slot * 66, 371, 64, 64);
    }

    public static Rectangle Reject(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(438 + slot * 66, 436, 66, 24);
    }

    public static IReadOnlyList<HireDockEntry?> Project(
        IReadOnlyList<short> offers,
        PendingHireState? pending,
        int? pendingSlot)
    {
        ArgumentNullException.ThrowIfNull(offers);
        var result = new HireDockEntry?[SlotCount];
        var reserved = pending is null ? -1 : Math.Clamp(pendingSlot ?? 0, 0, SlotCount - 1);
        if (pending is not null)
            result[reserved] = new HireDockEntry(pending.GangDefinitionId, true);
        var offerIndex = 0;
        for (var slot = 0; slot < SlotCount && offerIndex < offers.Count; slot++)
        {
            if (slot == reserved) continue;
            result[slot] = new HireDockEntry(offers[offerIndex++], false);
        }
        return result;
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }
}

public static class HireComparisonLayout
{
    public static Rectangle Panel => new(0, 0, 344, 209);
    public static Rectangle Ok => new(32, 168, 50, 24);

    public static Rectangle Portrait(int slot)
    {
        if (slot is < 0 or >= HireDockLayout.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(164 + slot * 41, 10, 32, 32);
    }

    public static Vector2 StatPosition(int slot, int row)
    {
        if (slot is < 0 or >= HireDockLayout.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return new Vector2(166 + slot * 41, 49 + row * 9);
    }

    public static bool IsBestValue(int row, short value, IEnumerable<short> comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        var values = comparison.ToArray();
        if (values.Length == 0) throw new ArgumentException("At least one value is required.", nameof(comparison));
        return row == 1 ? value == values.Min() : value == values.Max();
    }
}

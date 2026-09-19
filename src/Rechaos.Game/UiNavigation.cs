using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class BoundedPageNavigation
{
    public static int Move(int current, int count, int delta)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (current < 0 || current >= count) throw new ArgumentOutOfRangeException(nameof(current));
        return Math.Clamp(current + delta, 0, count - 1);
    }
}

public static class PointerButtonEdges
{
    public static bool Pressed(ButtonState current, ButtonState previous) =>
        current == ButtonState.Pressed && previous == ButtonState.Released;
}

public static class OriginalFontLayout
{
    public const char FirstCharacter = ' ';
    public const char LastCharacter = 'Z';
    public const int CellWidth = 6;
    public const int GlyphHeight = 7;
    public const int LineHeight = 9;
    public static Rectangle AtlasBounds => new(0, 0,
        (LastCharacter - FirstCharacter + 1) * CellWidth, GlyphHeight);

    public static bool TryGlyph(char character, out Rectangle source)
    {
        character = char.ToUpperInvariant(character);
        if (character is < FirstCharacter or > LastCharacter)
        {
            source = Rectangle.Empty;
            return false;
        }

        source = new Rectangle((character - FirstCharacter) * CellWidth, 0,
            CellWidth, GlyphHeight);
        return true;
    }
}

public static class SetupButtonLayout
{
    public static Rectangle AddPlayer => new(370, 328, 92, 24);
    public static Rectangle RemovePlayer => new(468, 328, 92, 24);
    public static Rectangle Start => new(370, 375, 92, 45);
    public static Rectangle Back => new(468, 375, 92, 45);

    public static Rectangle PressedSource(SetupPushButton button) => button switch
    {
        SetupPushButton.AddPlayer => new Rectangle(220, 0, 92, 24),
        SetupPushButton.RemovePlayer => new Rectangle(220, 24, 92, 24),
        SetupPushButton.Start => new Rectangle(220, 48, 92, 45),
        SetupPushButton.Back => new Rectangle(220, 93, 92, 45),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    public static Rectangle Destination(SetupPushButton button) => button switch
    {
        SetupPushButton.AddPlayer => AddPlayer,
        SetupPushButton.RemovePlayer => RemovePlayer,
        SetupPushButton.Start => Start,
        SetupPushButton.Back => Back,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    public static SetupPushButton? HitTest(Point point)
    {
        foreach (var button in Enum.GetValues<SetupPushButton>())
            if (Destination(button).Contains(point)) return button;
        return null;
    }
}

public enum SetupPushButton
{
    AddPlayer,
    RemovePlayer,
    Start,
    Back
}

public enum ClientScreen
{
    Title,
    Options,
    Help,
    Setup,
    Online,
    Lobby,
    City,
    GameInfo,
    Commands,
    Hire,
    Events,
    ComlinkView,
    ComlinkSend,
    Sector,
    SectorGangs,
    Gang,
    Site,
    ItemInformation,
    Finance,
    Ranking,
    Items,
    Give,
    GiveTarget,
    Sell,
    CombatSummary,
    Search,
    Handoff,
    Elimination,
    Endgame
}

public sealed class ScreenRouter
{
    public ClientScreen Current { get; private set; } = ClientScreen.Title;
    public event Action<ClientScreen, ClientScreen>? Changed;

    public void Show(ClientScreen screen)
    {
        if (screen == Current) return;
        var previous = Current;
        Current = screen;
        Changed?.Invoke(previous, screen);
    }

    public bool Back()
    {
        if (Current == ClientScreen.Title) return false;
        var destination = Current is ClientScreen.GameInfo or ClientScreen.Events or ClientScreen.ComlinkView
            or ClientScreen.ComlinkSend or ClientScreen.Commands or ClientScreen.Hire
            or ClientScreen.Sector or ClientScreen.SectorGangs or ClientScreen.Gang
            or ClientScreen.Finance or ClientScreen.Ranking
            or ClientScreen.Site
            or ClientScreen.ItemInformation
            or ClientScreen.Items or ClientScreen.Give or ClientScreen.GiveTarget or ClientScreen.Sell
            or ClientScreen.CombatSummary
            or ClientScreen.Search
            ? Current is ClientScreen.Give or ClientScreen.Sell ? ClientScreen.Items
                : Current == ClientScreen.GiveTarget ? ClientScreen.Give : ClientScreen.City
            : ClientScreen.Title;
        Show(destination);
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

public static partial class CityConsoleLayout;

public static class HandoffLayout
{
    public static Rectangle Panel => new(266, 148, 108, 164);
    public static Rectangle Portrait => new(280, 170, 80, 77);
    public static Rectangle Ready => new(266, 246, 108, 66);
    public const int NameY = 194;
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

/// <summary>
/// Tracks how long the pointer has rested on one hover region so a tooltip can wait out
/// a dwell delay instead of appearing the moment the cursor crosses a row.
/// </summary>
public sealed class HoverDwellTracker
{
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    private int? _region;
    private TimeSpan _enteredAt;

    /// <summary>The region the pointer has rested on for at least <see cref="Delay"/>, if any.</summary>
    public int? SettledRegion { get; private set; }

    /// <summary>Records the region under the pointer; null whenever no region is hovered.</summary>
    public void Update(int? region, TimeSpan timestamp)
    {
        if (region is < 0) throw new ArgumentOutOfRangeException(nameof(region));
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        if (region != _region || timestamp < _enteredAt)
        {
            _region = region;
            _enteredAt = timestamp;
        }

        SettledRegion = region is not null && timestamp - _enteredAt >= Delay ? region : null;
    }

    public void Cancel()
    {
        _region = null;
        SettledRegion = null;
    }
}

/// <summary>Native layout of the 8x8 sector cells in the PX10000-PX10006 city layers.</summary>
public static class CityMapLayout
{
    public const int Left = 2;
    public const int Top = 44;
    public const int GridInsetX = 4;
    public const int GridInsetY = 3;
    public const int ColumnStride = 53;
    public const int RowStride = 51;
    public const int TileWidth = 54;
    public const int TileHeight = 52;
    public static Rectangle Bounds => new(
        Left, Top, TileWidth * MatchLimits.BoardWidth, TileHeight * MatchLimits.BoardWidth);

    public static Rectangle Source(int sectorId)
    {
        ValidateSector(sectorId);
        return new Rectangle(
            GridInsetX + sectorId % 8 * ColumnStride,
            GridInsetY + sectorId / 8 * RowStride,
            TileWidth,
            TileHeight);
    }

    public static Rectangle Destination(int sectorId)
    {
        var source = Source(sectorId);
        return new Rectangle(Left + source.X, Top + source.Y, source.Width, source.Height);
    }

    // Every PX1000x cell contains its own copy of the green grid edge. Keep the
    // neutral sheet's grid fixed and replace only the artwork inside an owned cell.
    public static Rectangle OwnershipSource(int sectorId) => Inset(Source(sectorId));
    public static Rectangle OwnershipDestination(int sectorId) => Inset(Destination(sectorId));

    public static int OwnershipSheet(PlayerId? owner) => owner?.Value + 1 ?? 0;

    public static bool TrySectorAt(Point point, out int sectorId)
    {
        var x = point.X - Left - GridInsetX;
        var y = point.Y - Top - GridInsetY;
        if (x < 0 || x > ColumnStride * 8 || y < 0 || y > RowStride * 8)
        {
            sectorId = -1;
            return false;
        }
        var column = Math.Min(x / ColumnStride, MatchLimits.BoardWidth - 1);
        var row = Math.Min(y / RowStride, MatchLimits.BoardWidth - 1);
        sectorId = row * MatchLimits.BoardWidth + column;
        return true;
    }

    private static void ValidateSector(int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
    }

    private static Rectangle Inset(Rectangle rectangle) => new(
        rectangle.X + 1, rectangle.Y + 1, rectangle.Width - 2, rectangle.Height - 2);
}

/// <summary>Native scenarios and sectors receiving the PX00129 objective-pylon overlay.</summary>
public static class ObjectiveSectorMarkerPresentation
{
    private static readonly int[] BigManSectors = [27, 28, 35, 36];

    public static bool IsMarked(ScenarioId scenario, int sectorId, bool isImportant)
    {
        _ = CityMapLayout.Source(sectorId);
        return scenario == ScenarioId.Siege && isImportant
            || scenario == ScenarioId.BigMan && BigManSectors.Contains(sectorId);
    }
}

public static partial class OriginalSpriteLayout
{
    public const int ActivePlayerMarkerFrameCount = 12;
    public static Rectangle PolicePatrolCar => new(116, 0, 48, 64);
    public static Rectangle HiredStamp => new(120, 300, 60, 60);
    public static Rectangle SetupDragFrame => new(150, 386, 40, 40);
    public static Rectangle ObjectiveSectorPylons => new(344, 15, 54, 52);
    public static Rectangle SectorBackArrow => new(120, 211, 30, 47);

    public static Rectangle ActivePlayerMarker(int frame)
    {
        if (frame is < 0 or >= ActivePlayerMarkerFrameCount)
            throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle(frame * 20, 626, 20, 20);
    }

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

    public static Rectangle ItemPortrait(int definitionId)
    {
        if (definitionId is < 0 or >= 64) throw new ArgumentOutOfRangeException(nameof(definitionId));
        return new Rectangle(0, definitionId * 20, 20, 20);
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

public static partial class SectorDetailLayout
{
    public const int Left = 61;
    public const int Top = 48;
    public const int Columns = 3;
    public const int Rows = 3;
    public static Rectangle Back => new(4, 394, 28, 66);
    public static Rectangle Workspace => new(32, 42, 406, 418);

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
        return new Rectangle(portrait.X + 10, portrait.Y + 59, 100, 3);
    }

    public static Color SiteControlColor(PlayerId? influencedBy, PlayerId viewer) =>
        influencedBy is { } owner && owner != viewer
            ? new Color(190, 0, 220)
            : new Color(0, 247, 0);

    public static PlayerId? SiteControlOwner(
        PlayerId? influencedBy,
        PlayerId? sectorOwner,
        int resistance)
    {
        if (resistance < 0) throw new ArgumentOutOfRangeException(nameof(resistance));
        return influencedBy ?? (resistance == 0 ? sectorOwner : null);
    }
}

public static class SectorGangDropTarget
{
    public static GangId? EnemyAt(
        IReadOnlyList<MatchGangState> visibleGangs,
        PlayerId actorOwner,
        Point point)
    {
        ArgumentNullException.ThrowIfNull(visibleGangs);
        var displayed = visibleGangs
            .OrderBy(gang => gang.Owner == actorOwner ? 0 : 1)
            .ThenBy(gang => gang.Id.Value)
            .Take(SectorGangCardLayout.VisibleCards)
            .ToArray();
        var slot = Enumerable.Range(0, displayed.Length)
            .FirstOrDefault(index => SectorGangCardLayout.Frame(index).Contains(point), -1);
        return slot >= 0 && displayed[slot].Owner != actorOwner ? displayed[slot].Id : null;
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
        return new Rectangle(256, 70 + index * 24, 158, 22);
    }

    public static Rectangle TargetRow(int index)
    {
        if (index is < 0 or >= 13) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(226, 100 + index * 20, 198, 18);
    }

    public static bool OpensTargetPicker(GangAction action) => action is
        GangAction.Attack or GangAction.Equip or GangAction.Give or GangAction.Influence
        or GangAction.Move or GangAction.Research or GangAction.Sell;

    public static IReadOnlyList<GangAction> ActionsFor(bool recurring) => recurring
        ? Actions.Where(action => action == GangAction.None || CommandRules.CanRepeat(action)).ToArray()
        : Actions;
}

public static class GangInformationLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static int LeftValueRight => SharedPanelLayout.X(183);
    public static int RightValueRight => SharedPanelLayout.X(279);

    public static Rectangle Equipment(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(290, 21 + slot * 64, 40, 40);
    }

    public static int StatisticY(int row) => row switch
    {
        0 => SharedPanelLayout.Y(119),
        1 => SharedPanelLayout.Y(128),
        2 => SharedPanelLayout.Y(146),
        3 => SharedPanelLayout.Y(155),
        4 => SharedPanelLayout.Y(164),
        5 => SharedPanelLayout.Y(173),
        6 => SharedPanelLayout.Y(182),
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };
}

public static class SiteInformationLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => SharedPanelLayout.At(28, 15, 120, 64);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static int DataValueRight => SharedPanelLayout.X(279);
    public static int LeftValueRight => SharedPanelLayout.X(183);
    public static int RightValueRight => SharedPanelLayout.X(279);
    public static int DataY(int row)
    {
        if (row is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => SharedPanelLayout.Y(45),
            1 => SharedPanelLayout.Y(63),
            2 => SharedPanelLayout.Y(72),
            3 => SharedPanelLayout.Y(81),
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
    public static int StatisticY(int row)
    {
        if (row is < 0 or >= 7) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => SharedPanelLayout.Y(120),
            1 => SharedPanelLayout.Y(129),
            2 => SharedPanelLayout.Y(147),
            3 => SharedPanelLayout.Y(156),
            4 => SharedPanelLayout.Y(165),
            5 => SharedPanelLayout.Y(174),
            6 => SharedPanelLayout.Y(183),
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
}

public static class ItemInformationLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => SharedPanelLayout.At(34, 17, 48, 48);
    public static Rectangle CompactPortrait => SharedPanelLayout.At(48, 31, 20, 20);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public const int DescriptionColumns = 29;
    public static int LeftValueRight => SharedPanelLayout.X(183);
    public static int RightValueRight => SharedPanelLayout.X(279);
    public static int StatisticY(int row) => GangInformationLayout.StatisticY(row);

    public static string TypeLabel(int itemType) => itemType switch
    {
        0 => "STRENGTH",
        1 => "BLADE",
        2 => "RANGE",
        3 => "ARMOR",
        4 => "MISC",
        _ => throw new ArgumentOutOfRangeException(nameof(itemType))
    };
}

public static class ComlinkViewLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Page => SharedPanelLayout.At(29, 9, 59, 13);
    // Native view handler 0x0045d61a uses half-open panel-local rectangles
    // (31,33)-(57,56), (59,33)-(85,56), and (33,169)-(82,191).
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Date => SharedPanelLayout.At(94, 20, 238, 7);
    public static Rectangle SenderPortrait => SharedPanelLayout.At(111, 46, 64, 64);
    public static Rectangle SenderName => SharedPanelLayout.At(181, 46, 151, 7);
    public static Rectangle Message => SharedPanelLayout.At(94, 123, 238, 34);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
}

public static class ComlinkSendLayout
{
    public const int MessageColumns = 40;
    public const int MessageRows = 4;
    public const int TextRowStride = 8;
    public const int InverseCaretGlyphY = 441;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Point TextOrigin => new(199, 256);
    public static Rectangle Message => new(TextOrigin.X, TextOrigin.Y,
        MessageColumns * OriginalFontLayout.CellWidth,
        (MessageRows - 1) * TextRowStride + OriginalFontLayout.GlyphHeight);
    public static Rectangle Cancel => SharedPanelLayout.At(33, 137, 49, 22);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
    // Native press helper 0x00418821 draws a one-pixel-larger held sprite than
    // either half-open activation target, then restores the baked face on exit.
    public static Rectangle CancelPressed => new(Cancel.X, Cancel.Y, 50, 23);
    public static Rectangle OkPressed => new(Ok.X, Ok.Y, 50, 23);
    public static Rectangle CancelPressedSource => new(50, 409, 50, 23);
    public static Rectangle OkPressedSource => new(50, 386, 50, 23);

    /// <summary>Native Send caret destination for a cell in the fixed 4-by-40 editor.</summary>
    public static Rectangle CaretDestination(int column, int row)
    {
        ValidateEditorCell(column, row);
        return new Rectangle(TextOrigin.X + column * OriginalFontLayout.CellWidth,
            TextOrigin.Y + row * TextRowStride,
            OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);
    }

    /// <summary>
    /// PX00129 source used by native caret helper <c>0x0046023c</c>. The normal
    /// glyph strip is row zero; the same glyphs at y=441 carry the inverse cell.
    /// </summary>
    public static Rectangle CaretSource(char character, bool inverse)
    {
        if (!OriginalFontLayout.TryGlyph(character, out var glyph))
            throw new ArgumentOutOfRangeException(nameof(character));
        return new Rectangle(glyph.X, inverse ? InverseCaretGlyphY : glyph.Y,
            glyph.Width, glyph.Height);
    }

    public static Rectangle Recipient(int slot)
    {
        if (slot is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        // Send renderer 0x0045fdf1 fills a 105-by-34 backing tile. The
        // panel-buffer points (97/218, 163/197/231) translate to these final
        // screen coordinates as the 344-pixel form enters from the right.
        return new Rectangle(201 + slot / 3 * 121, 143 + slot % 3 * 34, 105, 34);
    }

    /// <summary>
    /// Native Send handler 0x0045eab1's half-open recipient click target.
    /// This intentionally includes each recipient's name/text region rather
    /// than only the smaller portrait cell returned by <see cref="Recipient"/>.
    /// </summary>
    public static Rectangle RecipientHit(int slot)
    {
        if (slot is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(98 + slot / 3 * 121, 20 + slot % 3 * 34, 100, 32);
    }

    public static Rectangle RecipientPortrait(int slot)
    {
        var cell = Recipient(slot);
        return new Rectangle(cell.X + 9, cell.Y + 1, 32, 32);
    }

    public static Rectangle RecipientAccent(int slot)
    {
        var cell = Recipient(slot);
        return new Rectangle(cell.X + 2, cell.Y + 2, 7, 30);
    }

    public static Point RecipientNameOrigin(int slot)
    {
        var cell = Recipient(slot);
        return new Point(cell.X + 42, cell.Y + 2);
    }

    private static void ValidateEditorCell(int column, int row)
    {
        if (column is < 0 or >= MessageColumns) throw new ArgumentOutOfRangeException(nameof(column));
        if (row is < 0 or >= MessageRows) throw new ArgumentOutOfRangeException(nameof(row));
    }
}

public static class InfluenceCommandLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle Site(int slot) => slot switch
    {
        0 => SharedPanelLayout.At(105, 16, 120, 64),
        1 => SharedPanelLayout.At(208, 73, 120, 64),
        2 => SharedPanelLayout.At(105, 130, 120, 64),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    /// <summary>
    /// Native Influence handler 0x0043f692's half-open site selection targets.
    /// These differ slightly from the staggered card artwork apertures.
    /// </summary>
    public static Rectangle SiteHit(int slot) => slot switch
    {
        0 => SharedPanelLayout.At(106, 17, 120, 64),
        1 => SharedPanelLayout.At(208, 73, 120, 64),
        2 => SharedPanelLayout.At(106, 127, 120, 64),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}

public static class AttackCommandLayout
{
    public const int VisibleTargets = 6;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle ActorPortrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle ActorForceBar => SharedPanelLayout.At(26, 103, 64, 3);

    public static Rectangle ActorItem(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(26 + slot * 22, 82, 20, 20);
    }

    public static Rectangle Opponent(int slot)
    {
        if (slot is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(98, 16 + slot * 36, 32, 32);
    }

    public static Rectangle TargetCard(int targetSlot)
    {
        var portrait = TargetPortrait(targetSlot);
        return new Rectangle(portrait.X, portrait.Y, 64, 90);
    }

    /// <summary>
    /// Native Attack handler 0x0043b290 partitions one six-cell target region
    /// for pointer selection; its regions are wider than the gang-card art.
    /// </summary>
    public static Rectangle TargetHit(int targetSlot)
    {
        if (targetSlot is < 0 or >= VisibleTargets)
            throw new ArgumentOutOfRangeException(nameof(targetSlot));
        var column = targetSlot % 3;
        var row = targetSlot / 3;
        var x = column switch { 0 => 135, 1 => 202, _ => 270 };
        var width = column switch { 0 => 67, 1 => 68, _ => 67 };
        return SharedPanelLayout.At(x, 16 + row * 89, width, row == 0 ? 89 : 88);
    }

    public static Rectangle TargetPortrait(int targetSlot)
    {
        if (targetSlot is < 0 or >= VisibleTargets)
            throw new ArgumentOutOfRangeException(nameof(targetSlot));
        return SharedPanelLayout.At(136 + targetSlot % 3 * 66,
            16 + targetSlot / 3 * 90, 64, 64);
    }

    public static Rectangle TargetForceBar(int targetSlot)
    {
        var portrait = TargetPortrait(targetSlot);
        return new Rectangle(portrait.X, portrait.Y + 87, 64, 3);
    }

    public static Rectangle TargetItem(int targetSlot, int itemSlot)
    {
        if (itemSlot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(itemSlot));
        var portrait = TargetPortrait(targetSlot);
        return new Rectangle(portrait.X + itemSlot * 22, portrait.Y + 66, 20, 20);
    }
}

public static class AttackTargetRoster
{
    /// <summary>
    /// Limits attack presentation to detectable gangs in the actor's sector,
    /// then keeps every owner's gangs contiguous and in portrait-selector order.
    /// </summary>
    public static IReadOnlyList<GameCommand> Order(
        MatchState state,
        IEnumerable<GameCommand> commands) => commands
        .Select(command => (command,
            actor: state.FindGang(command.Gang),
            target: state.FindGang(new GangId(command.Target.Id))))
        .Where(entry => entry.actor is not null
            && entry.target is { IsActive: true }
            && entry.target.SectorId == entry.actor.SectorId
            && state.CanPlayerDetectGang(entry.command.Player, entry.target.Id))
        .OrderBy(entry => entry.target!.Owner.Value)
        .ThenBy(entry => entry.target!.Id.Value)
        .Select(entry => entry.command)
        .ToArray();
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

public static partial class PlayerPortraitLayout
{
    public const int Count = 16;
    public const int SelectableCount = 15;

    /// <summary>The label the Sector workspace flags a gang-holding opponent with.</summary>
    public const string GangPresenceLabel = "GANGS";

    public static Rectangle SetupTop(int player)
    {
        Validate(player);
        return new Rectangle(360 + player * 36, 38, 32, 32);
    }

    public static Rectangle CityTop(int player)
    {
        Validate(player);
        return new Rectangle(16 + player * 72, 4, 32, 32);
    }

    public static Rectangle CityActiveMarker(int player)
    {
        Validate(player);
        return new Rectangle(48 + player * 72, 4, 20, 20);
    }

    /// <summary>
    /// The strip under a city-row portrait carrying <see cref="GangPresenceLabel"/>. One glyph row
    /// starting a pixel into the portrait stops exactly on the Sector workspace's top edge.
    /// </summary>
    public static Rectangle CityGangPresence(int player)
    {
        var portrait = CityTop(player);
        return new Rectangle(
            portrait.X + 1,
            portrait.Bottom - 1,
            GangPresenceLabel.Length * OriginalFontLayout.CellWidth,
            OriginalFontLayout.GlyphHeight);
    }

    /// <summary>
    /// Where a caption of <paramref name="columns"/> characters goes under a top-bar portrait.
    /// </summary>
    /// <remarks>
    /// The eight rows between the portraits and the top of the map at y 44 are all the space the
    /// top bar has, so a caption takes one glyph row of it and is centred on the portrait it
    /// belongs to rather than on the wider cell the portrait shares with the active-player marker.
    /// </remarks>
    public static Rectangle CityCaption(int player, int columns)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var portrait = CityTop(player);
        var width = columns * OriginalFontLayout.CellWidth;
        return new Rectangle(
            portrait.X + (portrait.Width - width) / 2,
            portrait.Bottom + 1,
            width,
            OriginalFontLayout.GlyphHeight);
    }

    public static Rectangle SetupLarge(int player) => Player(player, 397, 89, 83, 64, 64, rowStride: 74);
    public static Rectangle Previous(int player) => Player(player, 399, 109, 83, 12, 18, rowStride: 74);
    public static Rectangle Next(int player) => Player(player, 447, 109, 83, 12, 18, rowStride: 74);
    public static Rectangle Name(int player) => Player(player, 397, 153, 83, 64, 8, rowStride: 74);

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

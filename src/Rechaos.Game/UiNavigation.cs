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

    public static bool Released(ButtonState current, ButtonState previous) =>
        current == ButtonState.Released && previous == ButtonState.Pressed;
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

    /// <summary>The plain font strip of PX00129, at its top-left corner.</summary>
    public static Point PlainStrip => new(0, 0);

    /// <summary>
    /// FND-UI-019, FND-SEARCH-004: the darker strip at (152,274), laid out as the plain one.
    /// </summary>
    public static Point DimStrip => new(152, 274);

    /// <summary>
    /// Width of the glyph mask <see cref="PixelFont"/> builds: the original strip followed by one
    /// cell per <see cref="SupplementalFontGlyphs"/> character.
    /// </summary>
    public static int MaskWidth => AtlasBounds.Width + SupplementalFontGlyphs.Characters.Length * CellWidth;

    /// <summary>Source cell of <paramref name="character"/> in the glyph mask.</summary>
    /// <remarks>
    /// U+2212 MINUS SIGN is drawn as the hyphen-minus the strip has. Cultures whose number format
    /// uses it (Swedish, Norwegian and others under ICU) otherwise lost the sign of every negative
    /// number on screen. The game formats with the invariant culture (<see cref="GameCulture"/>);
    /// this keeps a stray culture-formatted number readable all the same.
    /// </remarks>
    public static bool TryGlyph(char character, out Rectangle source)
    {
        if (character == '\u2212') character = '-';
        character = char.ToUpperInvariant(character);
        if (character is >= FirstCharacter and <= LastCharacter)
        {
            source = Cell((character - FirstCharacter) * CellWidth);
            return true;
        }

        var supplemental = SupplementalFontGlyphs.Characters.IndexOf(character);
        source = supplemental < 0
            ? Rectangle.Empty
            : Cell(AtlasBounds.Width + supplemental * CellWidth);
        return supplemental >= 0;
    }

    private static Rectangle Cell(int x) => new(x, 0, CellWidth, GlyphHeight);
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

    /// <summary>
    /// The window pixels a virtual rectangle covers, for a scissor rectangle. Both edges are
    /// rounded to the nearest pixel, so two rectangles that share an edge share it on screen too.
    /// </summary>
    public static Rectangle ToPhysical(Viewport viewport, Rectangle area)
    {
        var scale = MathF.Min(viewport.Width / (float)Width, viewport.Height / (float)Height);
        var left = (viewport.Width - Width * scale) / 2;
        var top = (viewport.Height - Height * scale) / 2;
        var x0 = (int)MathF.Round(left + area.Left * scale);
        var y0 = (int)MathF.Round(top + area.Top * scale);
        var x1 = (int)MathF.Round(left + area.Right * scale);
        var y1 = (int)MathF.Round(top + area.Bottom * scale);
        return new Rectangle(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
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

/// <summary>SCR-SETUP-002, FND-SETUP-016: the hand-off card and what is drawn on it.</summary>
public static class HandoffLayout
{
    public static Rectangle Panel => new(266, 130, 108, 164);
    public static Rectangle ColourBar => new(283, 155, 8, 72);
    public static Rectangle NameBacking => new(293, 155, 60, 7);
    public static Point Name => new(293, 155);
    public static Rectangle Portrait => new(293, 163, 64, 64);
    public static Rectangle Ready => new(270, 241, 100, 48);
    public static Rectangle ReadyPressedSource => new(388, 512, 100, 48);
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

/// <summary>
/// The city map of SCR-UI-003: the prepared map surface copied to <c>(2,42)</c>, with the 8-by-8
/// grid of 54-by-52 cells on a 53-by-51 stride from map <c>(4,3)</c> (FND-UI-017, FND-UI-033).
/// </summary>
public static class CityMapLayout
{
    public const int Left = 2;
    public const int Top = 42;
    public const int GridInsetX = 4;
    public const int GridInsetY = 3;
    public const int ColumnStride = 53;
    public const int RowStride = 51;
    public const int TileWidth = 54;
    public const int TileHeight = 52;
    public const int SelectionFrameCount = 2;
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

    /// <summary>
    /// The black bands over the cells of a three-by-three crop of the map around
    /// <paramref name="centerSectorId"/> that lie beyond the city's edge. The top row and left
    /// column end at <paramref name="nearEdge"/> and the bottom row and right column start at
    /// <paramref name="farStart"/>, both from <paramref name="area"/>'s corner, as each caller's
    /// evidence places them (FND-MOVE-004, FND-UI-018).
    /// </summary>
    public static IReadOnlyList<Rectangle> OffMapBands(
        int centerSectorId, Rectangle area, Point nearEdge, Point farStart)
    {
        ValidateSector(centerSectorId);
        var column = centerSectorId % MatchLimits.BoardWidth;
        var row = centerSectorId / MatchLimits.BoardWidth;
        var last = MatchLimits.BoardWidth - 1;
        var bands = new List<Rectangle>(2);
        if (row == 0) bands.Add(new Rectangle(area.X, area.Y, area.Width, nearEdge.Y));
        if (row == last)
            bands.Add(new Rectangle(area.X, area.Y + farStart.Y, area.Width, area.Height - farStart.Y));
        if (column == 0) bands.Add(new Rectangle(area.X, area.Y, nearEdge.X, area.Height));
        if (column == last)
            bands.Add(new Rectangle(area.X + farStart.X, area.Y, area.Width - farStart.X, area.Height));
        return bands;
    }

    // Every PX1000x cell contains its own copy of the green grid edge. Keep the
    // neutral sheet's grid fixed and replace only the artwork inside an owned cell.
    public static Rectangle OwnershipSource(int sectorId) => Inset(Source(sectorId));
    public static Rectangle OwnershipDestination(int sectorId) => Inset(Destination(sectorId));

    public static int OwnershipSheet(PlayerId? owner) => owner?.Value + 1 ?? 0;

    /// <summary>A rectangle of the screen's map at <c>(2,42)</c> in map coordinates.</summary>
    public static Rectangle MapArea(Rectangle screen) =>
        screen with { X = screen.X - Left, Y = screen.Y - Top };

    /// <summary>
    /// FND-UI-015: a press on the map takes the sector <c>(x - 2) / 54 + ((y - 42) / 52) * 8</c>,
    /// a grid that drifts up to four pixels from the drawn 53-by-51 one toward the bottom right.
    /// </summary>
    public static bool TrySectorAt(Point point, out int sectorId)
    {
        if (!Bounds.Contains(point))
        {
            sectorId = -1;
            return false;
        }
        sectorId = (point.X - Left) / TileWidth + (point.Y - Top) / TileHeight * MatchLimits.BoardWidth;
        return true;
    }

    /// <summary>
    /// FND-UI-017: the keyed selection frame, <c>f</c> being the pump's counter <c>0x00487804</c>
    /// divided by 4. The counter steps once a presentation tick and wraps at 8 (FND-EVENT-006).
    /// </summary>
    public static Rectangle SelectionFrameSource(int frame)
    {
        if (frame is < 0 or >= SelectionFrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle(236 + frame * TileWidth, 15, TileWidth, TileHeight);
    }

    public static int SelectionFrame(TimeSpan now) => (int)(PresentationClock.Ticks(now) % 8 / 4);

    /// <summary>
    /// FND-UI-048: the frame on screen while the pump's counter stands at <paramref name="counter"/>,
    /// since the pump draws the frame before it advances the counter.
    /// </summary>
    public static int SelectionFrameAfterPass(int counter) => (counter + 7) % 8 / 4;

    /// <summary>
    /// FND-UI-017: the column letter tabs above and below the map and the row number tabs left and
    /// right of it, each with the glyph's offset inside the tab.
    /// </summary>
    public static IEnumerable<GridLabel> GridLabels()
    {
        for (var column = 0; column < MatchLimits.BoardWidth; column++)
        {
            var letter = ((char)('A' + column)).ToString();
            yield return new GridLabel(new Point(21 + column * ColumnStride, 42),
                new Rectangle(276, 448, 23, 13), new Point(9, 1), letter);
            yield return new GridLabel(new Point(21 + column * ColumnStride, 444),
                new Rectangle(276, 461, 23, 13), new Point(9, 5), letter);
        }
        for (var row = 0; row < MatchLimits.BoardWidth; row++)
        {
            var number = (row + 1).ToString();
            yield return new GridLabel(new Point(3, 59 + row * RowStride),
                new Rectangle(299, 448, 13, 23), new Point(1, 8), number);
            yield return new GridLabel(new Point(421, 59 + row * RowStride),
                new Rectangle(312, 448, 13, 23), new Point(7, 8), number);
        }
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

/// <summary>
/// FND-UI-050: the city map puts a sector's police badge at map <c>(53c + 9, 51r + 14)</c>, 5
/// pixels right of and 11 below the corner of its cell.
/// </summary>
public static class PoliceBadgeLayout
{
    public static Point CellOffset => new(9 - CityMapLayout.GridInsetX, 14 - CityMapLayout.GridInsetY);

    public static Rectangle Destination(int sectorId)
    {
        var sector = CityMapLayout.Destination(sectorId);
        return new Rectangle(sector.X + CellOffset.X, sector.Y + CellOffset.Y, 20, 28);
    }
}

public static partial class OriginalSpriteLayout
{
    public const int ActivePlayerMarkerFrameCount = 12;
    /// <summary>SCR-HIRE-002, FND-HIRE-008: the 64-by-64 hire and snub marks.</summary>
    public static Rectangle HiredStamp => new(114, 299, 64, 64);
    public static Rectangle SnubbedStamp => new(178, 299, 64, 64);
    public static Rectangle SetupDragFrame => new(150, 386, 40, 40);
    public static Rectangle ObjectiveSectorPylons => new(344, 15, 54, 52);
    /// <summary>FND-UI-050: the badge of a sector under police presence.</summary>
    public static Rectangle PoliceBadge => new(317, 560, 20, 28);

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

/// <summary>A keyed tab of the grid's edge labels and the glyph written into it.</summary>
public readonly record struct GridLabel(Point Destination, Rectangle Source, Point GlyphOffset, string Text);

/// <summary>
/// FND-UI-017: the gang-status marker sits at map <c>(33 + 53c, 19 + 51r)</c>, 29 pixels right
/// of and 16 below the corner of its cell.
/// </summary>
public static class GangStatusMarkerLayout
{
    public static Point CellOffset => new(29, 16);

    public static Rectangle Destination(int sectorId)
    {
        var sector = CityMapLayout.Destination(sectorId);
        return new Rectangle(sector.X + CellOffset.X, sector.Y + CellOffset.Y, 20, 20);
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
        // The card a press here would take (FND-UI-015), gaps between the cards included.
        var slot = SectorGangCardLayout.CardAt(point);
        return slot >= 0 && slot < displayed.Length && displayed[slot].Owner != actorOwner
            ? displayed[slot].Id
            : null;
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

    /// <summary>
    /// FND-UI-021: menu 1 greys only Attack, Control, Influence, Heal, Sell and Give, so it never
    /// greys Equip, Move or Research, and their panels open with an empty list when the gang has
    /// nothing to choose (EXP-UI-011 shows it for Research).
    /// </summary>
    public static bool OpensWithoutTargets(GangAction action) =>
        action is GangAction.Equip or GangAction.Move or GangAction.Research;

    /// <summary>
    /// Whether the order panel offers <paramref name="action"/>, given the orders the rules allow:
    /// None always (RULE-TURN-005, EXP-TURN-095), any order with a legal command, and for one gang
    /// an order whose panel opens without targets.
    /// </summary>
    public static bool Offers(GangAction action, IEnumerable<GameCommand> options, bool singleGang) =>
        action == GangAction.None
        || options.Any(command => command.Action == action)
        || (singleGang && OpensWithoutTargets(action));

    public static IReadOnlyList<GangAction> ActionsFor(bool recurring) => recurring
        ? Actions.Where(action => action == GangAction.None || CommandRules.CanRepeat(action)).ToArray()
        : Actions;
}

public static class SiteInformationLayout
{
    // Native handler 0x0044C476 (FND-UI-005) uses the alternate PX05002 slide form.
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Portrait => new(156, 139, 120, 64);
    /// <summary>FND-UI-049: the frame keyed over the portrait, from <c>PX00129</c>.</summary>
    public static Rectangle PortraitFrameSource => new(242, 299, 120, 64);
    /// <summary>FND-UI-049: string <c>29 + special</c> for a site with a special effect.</summary>
    public static Vector2 SpecialLine => new(288, 214);
    public const int SpecialStringBase = 0x1D;
    public static Rectangle Ok => new(161, 293, 49, 22);
    public static int NameLeft => 288;
    public static int DataValueLeft => 396;
    public static int LeftValueLeft => 300;
    public static int RightValueLeft => 396;
    public static int DataLabelLeft => 286;
    public static int LeftStatisticLabelLeft => 222;
    public static int RightStatisticLabelLeft => 318;
    public static int DataY(int row)
    {
        if (row is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => 169,
            1 => 187,
            2 => 196,
            3 => 205,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
    public static int StatisticY(int row)
    {
        if (row is < 0 or >= 7) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
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
}

public static class ComlinkViewLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    // DEV-UI-005: the inbox tooltip's hover area, the panel art's frame around the number and
    // count, which holds both digit cells.
    public static Rectangle Page => SharedPanelLayout.At(29, 9, 59, 13);
    // FND-COMLINK-007: the View fields of fn_0045E04D, panel-local.
    public static Rectangle PageNumber => DigitCells(34, 13, 2);
    public static Rectangle PageCount => DigitCells(70, 13, 2);
    // Native view handler 0x0045D61A (FND-COMLINK-002) uses half-open panel-local rectangles
    // (31,33)-(57,56), (59,33)-(85,56), and (33,169)-(82,191).
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Year => DigitCells(95, 20, 4);
    public static Rectangle Week => DigitCells(125, 20, 2);
    public const int SenderNameColumns = 10;
    public static Rectangle SenderName => SharedPanelLayout.At(95, 38, 60, 7);
    public static Rectangle SenderColour => SharedPanelLayout.At(95, 46, 8, 64);
    public static Rectangle SenderPortrait => SharedPanelLayout.At(103, 46, 64, 64);
    public static Point MessageOrigin => new(SharedPanelLayout.X(95), SharedPanelLayout.Y(121));
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);

    private static Rectangle DigitCells(int x, int y, int count) =>
        SharedPanelLayout.At(x, y, count * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);
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
    // The held-button helper fn_00418821 (FND-COMLINK-011, FND-UI-062) draws a face one pixel
    // larger than either half-open activation target: the lit face while the pointer is over the
    // held button, and the plain face while it is off it and after the release.
    public static Rectangle CancelPressed => new(Cancel.X, Cancel.Y, 50, 23);
    public static Rectangle OkPressed => new(Ok.X, Ok.Y, 50, 23);
    public static Rectangle CancelPressedSource => HeldButtonFaces.Lit(HeldButtonKind.Cancel);
    public static Rectangle OkPressedSource => HeldButtonFaces.Lit(HeldButtonKind.Confirm);

    /// <summary>Native Send caret destination for a cell in the fixed 4-by-40 editor.</summary>
    public static Rectangle CaretDestination(int column, int row)
    {
        ValidateEditorCell(column, row);
        return new Rectangle(TextOrigin.X + column * OriginalFontLayout.CellWidth,
            TextOrigin.Y + row * TextRowStride,
            OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);
    }

    /// <summary>
    /// PX00129 source used by native caret helper <c>0x0046023C</c> (FND-COMLINK-010). The normal
    /// glyph strip is row zero; the same glyphs at y=441 carry the inverse cell.
    /// Only the original strip has that inverse row, so the supplemental glyphs
    /// drawn after it (the status console's brackets) have no caret cell.
    /// </summary>
    public static Rectangle CaretSource(char character, bool inverse)
    {
        if (!OriginalFontLayout.TryGlyph(character, out var glyph)
            || glyph.X >= OriginalFontLayout.AtlasBounds.Width)
            throw new ArgumentOutOfRangeException(nameof(character));
        return new Rectangle(glyph.X, inverse ? InverseCaretGlyphY : glyph.Y,
            glyph.Width, glyph.Height);
    }

    public static Rectangle Recipient(int slot)
    {
        if (slot is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        // Send renderer 0x0045FDF1 (FND-COMLINK-007) fills a 105-by-34 backing tile. The
        // panel-buffer points (97/218, 163/197/231) translate to these final
        // screen coordinates as the 344-pixel form enters from the right.
        return new Rectangle(201 + slot / 3 * 121, 143 + slot % 3 * 34, 105, 34);
    }

    /// <summary>
    /// FND-COMLINK-011: native Send handler 0x0045EAB1's half-open recipient click target.
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

    /// <summary>FND-COMLINK-007: the name at (+42, +2) from the card point, one inside the frame.</summary>
    public static Point RecipientNameOrigin(int slot)
    {
        var cell = Recipient(slot);
        return new Point(cell.X + 43, cell.Y + 3);
    }

    /// <summary>
    /// FND-COMLINK-007: the portrait of a slot that cannot be sent to comes from the row at y 594
    /// of PX00129, where the others come from y 480.
    /// </summary>
    public static Rectangle RecipientPortraitSource(int portraitId, bool eligible)
    {
        var source = OriginalSpriteLayout.OverlordPortrait(portraitId);
        return eligible ? source : new Rectangle(source.X, 594, source.Width, source.Height);
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

    /// <summary>
    /// FND-INFLUENCE-002: the site picture frame over a site that is not completed, the frame over
    /// a completed one, and the frame of the chosen site, all from <c>PX00129</c> keyed on white.
    /// </summary>
    public static Rectangle SiteFrameSource => new(242, 299, 120, 64);
    public static Rectangle CompletedSiteFrameSource => new(362, 299, 120, 64);
    public static Rectangle ChosenSiteFrameSource => new(0, 235, 120, 64);

    /// <summary>
    /// FND-INFLUENCE-001, FND-INFLUENCE-002: native Influence handler 0x0043F692's half-open site
    /// selection targets, which are also where it draws each slot's picture (EXP-UI-010).
    /// </summary>
    public static Rectangle SiteHit(int slot) => slot switch
    {
        0 => SharedPanelLayout.At(106, 17, 120, 64),
        1 => SharedPanelLayout.At(208, 73, 120, 64),
        2 => SharedPanelLayout.At(106, 127, 120, 64),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
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

    public static Rectangle SetupTop(int player)
    {
        Validate(player);
        return new Rectangle(360 + player * 36, 38, 32, 32);
    }

    public static Rectangle SetupLarge(int player) => Player(player, 397, 89, 83, 64, 64, rowStride: 74);
    public static Rectangle Previous(int player) => Player(player, 399, 109, 83, 12, 18, rowStride: 74);
    public static Rectangle Next(int player) => Player(player, 447, 109, 83, 12, 18, rowStride: 74);

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

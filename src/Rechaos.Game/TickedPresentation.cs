using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>What a <see cref="TickedPresentation"/> draws while it holds input.</summary>
public enum TickedPresentationKind
{
    /// <summary><c>fn_00418CCC</c>: a panel face drawn pressed for one wait (FND-UI-019).</summary>
    KeyFace,

    /// <summary><c>fn_0041ACE6</c>: a city cell flashed for a Hire drop (FND-UI-017, FND-UI-037).</summary>
    CityCellFlash,

    /// <summary><c>fn_00419AA8</c>: a site portrait flashed for an Influence order (FND-UI-018).</summary>
    SiteFlash,

    /// <summary><c>fn_0041A0D4</c>: a cell of the nine-sector display flashed (FND-UI-018).</summary>
    SectorDisplayCellFlash
}

/// <summary>What a flash's lit copy is made of, in the order it is drawn (FND-UI-037).</summary>
public enum FlashLayer
{
    /// <summary>The cell or site image the flash copies, with what that image already holds.</summary>
    Image,

    /// <summary>White through bitmap 143 over the lit area.</summary>
    Lightening,

    /// <summary>The frame keyed over a site image or the nine-sector display.</summary>
    Frame,

    /// <summary>The edge tabs or labels with the column letter and row digit.</summary>
    Labels,

    /// <summary>A site's progress meter, drawn when the sector's owner is the active player.</summary>
    Meter
}

/// <summary>The three pressed faces <c>fn_00418CCC</c> copies from PX00129 (FND-UI-019).</summary>
public enum PressedKeyFace
{
    /// <summary>Kind 0, the confirm or OK face.</summary>
    Confirm = 0,

    /// <summary>Kind 1, the Cancel face.</summary>
    Cancel = 1,

    /// <summary>Kind 3, the back control of the sector view.</summary>
    SectorBack = 3
}

public static class PressedKeyFaces
{
    /// <summary>FND-UI-019: the pressed image of each kind on the sheet PX00129.</summary>
    public static Rectangle Source(PressedKeyFace face) => face switch
    {
        PressedKeyFace.Confirm => new Rectangle(0, 386, 50, 23),
        PressedKeyFace.Cancel => new Rectangle(0, 409, 50, 23),
        PressedKeyFace.SectorBack => new Rectangle(120, 205, 32, 63),
        _ => throw new ArgumentOutOfRangeException(nameof(face))
    };

    /// <summary>Where the pressed image lands, from the top-left corner the caller passes.</summary>
    public static Rectangle Destination(PressedKeyFace face, Point topLeft)
    {
        var source = Source(face);
        return new Rectangle(topLeft.X, topLeft.Y, source.Width, source.Height);
    }
}

/// <summary>
/// One of the presentation steps that pause on the six-per-second clock (RULE-TIMER-004): a
/// pressed key face, or a cell or site flash. Each wait lasts until the next tick of
/// <see cref="PresentationClock"/>, so a one-tick wait lasts between almost 0 and 166 ms. The
/// original handles no input while it waits, and the game drops input for as long as one runs.
/// </summary>
public sealed class TickedPresentation
{
    private IReadOnlyList<bool>? _pattern;
    private long _startTick;
    private Action? _then;

    public TickedPresentationKind Kind { get; private set; }

    /// <summary>The virtual-screen rectangle the step draws over.</summary>
    public Rectangle Area { get; private set; }

    /// <summary>The PX00129 image a key face shows, or null for a flash.</summary>
    public Rectangle? Source { get; private set; }

    public bool Active => _pattern is not null;

    /// <summary>
    /// Whether the highlighted image shows during each wait, in order. The number of entries is
    /// the number of one-tick waits.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>fn_00418CCC</c> copies the pressed face, waits once and copies the released face
    /// (FND-UI-019).</item>
    /// <item><c>fn_0041ACE6</c>, <c>fn_00419AA8</c> and <c>fn_0041A0D4</c> each make four
    /// copies separated by three waits: lightened, normal, lightened, normal (FND-UI-037).</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<bool> LitPattern(TickedPresentationKind kind) => kind switch
    {
        TickedPresentationKind.KeyFace => [true],
        TickedPresentationKind.CityCellFlash or TickedPresentationKind.SiteFlash
            or TickedPresentationKind.SectorDisplayCellFlash => [true, false, true],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>
    /// FND-UI-037: the flashes lighten with white through bitmap 143, the pattern the grey 0x7FFF
    /// selects, anchored at the corner of the lightened area.
    /// </summary>
    public static int FlashPattern => OriginalPatternMask.ForGrey(0x7fff);

    /// <summary>
    /// The part of <paramref name="area"/> a flash lightens (FND-UI-037). A city cell or a cell
    /// of the nine-sector display is lightened from one pixel inside its 54-by-52 corner, 52 by
    /// 50; a site flash lightens the whole 120-by-64 site image.
    /// </summary>
    public static Rectangle LitArea(TickedPresentationKind kind, Rectangle area) => kind switch
    {
        TickedPresentationKind.CityCellFlash or TickedPresentationKind.SectorDisplayCellFlash =>
            new Rectangle(area.X + 1, area.Y + 1, 52, 50),
        TickedPresentationKind.SiteFlash => area,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>
    /// FND-UI-037: the order each flash builds its lit copy in. The image is lightened first and
    /// the labels, frame and meter are drawn over it afterwards, so they show unlit.
    /// </summary>
    public static IReadOnlyList<FlashLayer> Layers(TickedPresentationKind kind) => kind switch
    {
        TickedPresentationKind.CityCellFlash =>
            [FlashLayer.Image, FlashLayer.Lightening, FlashLayer.Labels],
        TickedPresentationKind.SiteFlash =>
            [FlashLayer.Image, FlashLayer.Lightening, FlashLayer.Frame, FlashLayer.Meter],
        TickedPresentationKind.SectorDisplayCellFlash =>
            [FlashLayer.Image, FlashLayer.Lightening, FlashLayer.Frame, FlashLayer.Labels],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>Starts a step; <paramref name="then"/> runs once its last wait ends.</summary>
    public void Start(
        TickedPresentationKind kind, Rectangle area, Rectangle? source, Action? then, TimeSpan now)
    {
        _pattern = LitPattern(kind);
        _startTick = PresentationClock.Ticks(now);
        _then = then;
        Kind = kind;
        Area = area;
        Source = source;
    }

    /// <summary>Whether the highlighted image is on screen at <paramref name="now"/>.</summary>
    public bool Lit(TimeSpan now)
    {
        if (_pattern is not { } pattern) return false;
        var wait = PresentationClock.Ticks(now) - _startTick;
        return wait >= 0 && wait < pattern.Count && pattern[(int)wait];
    }

    /// <summary>
    /// Ends the step once its waits are over and hands back what was to run after it.
    /// </summary>
    public bool TryFinish(TimeSpan now, out Action? then)
    {
        then = null;
        if (_pattern is not { } pattern
            || PresentationClock.Ticks(now) - _startTick < pattern.Count)
            return false;
        then = _then;
        Clear();
        return true;
    }

    /// <summary>Drops the step and whatever was to run after it.</summary>
    public void Clear()
    {
        _pattern = null;
        _then = null;
        Source = null;
    }
}

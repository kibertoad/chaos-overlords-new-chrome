using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>What a <see cref="TickedPresentation"/> draws while it holds input.</summary>
public enum TickedPresentationKind
{
    /// <summary><c>fn_00418CCC</c>: a panel face drawn pressed for one wait (FND-UI-019).</summary>
    KeyFace,

    /// <summary><c>fn_0041ACE6</c>: a city cell flashed for a Hire drop (FND-UI-017).</summary>
    CityCellFlash,

    /// <summary><c>fn_00419AA8</c>: a site portrait flashed for an Influence order (FND-UI-018).</summary>
    SiteFlash,

    /// <summary><c>fn_0041A0D4</c>: a cell of the nine-sector display flashed (FND-UI-018).</summary>
    SectorDisplayCellFlash
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
    /// <item><c>fn_0041ACE6</c> puts the lightened cell up, waits, puts the normal cell back, and
    /// does both once more (FND-UI-017). The normal cell is put back and replaced by the lightened
    /// one with no wait between them, so it shows for no time and the cell stays lit for two
    /// waits.</item>
    /// <item><c>fn_00419AA8</c> and <c>fn_0041A0D4</c> make four copies separated by waits:
    /// lightened, normal, lightened, normal (FND-UI-018).</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<bool> LitPattern(TickedPresentationKind kind) => kind switch
    {
        TickedPresentationKind.KeyFace => [true],
        TickedPresentationKind.CityCellFlash => [true, true],
        TickedPresentationKind.SiteFlash or TickedPresentationKind.SectorDisplayCellFlash =>
            [true, false, true],
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

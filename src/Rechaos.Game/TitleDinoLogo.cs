using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The Refurbished Dinosaurs logo in the title screen's lower left corner (DEV-UI-019). Only the
/// dinosaur head shows until the pointer rests on it; then the name slides out from behind the
/// head, and slides back in when the pointer leaves. A click on the visible logo opens the
/// project's site.
/// </summary>
internal sealed class TitleDinoLogo
{
    public const string Url = "https://dinorefurb.com/";

    /// <summary>The head, at the size of its bitmap, clear of the Options button above it and
    /// of the copyright line below.</summary>
    public static readonly Rectangle Head = new(8, 412, 34, 32);

    /// <summary>Where the name rests once it has come all the way out.</summary>
    public static readonly Rectangle Text = new(Head.X + 38, Head.Y, 106, 32);

    /// <summary>
    /// The name is drawn only right of this line, and the head over it, so the name comes out of
    /// the head's back rather than from the empty gap after it. The head's eye is left of the line.
    /// </summary>
    public static readonly int EmergeLine = Head.Right - 8;

    public static readonly TimeSpan RevealTime = TimeSpan.FromMilliseconds(420);

    /// <summary>
    /// The most one update moves the name, so a frame that arrives late, after the window was
    /// dragged or left in the background, does not skip the slide.
    /// </summary>
    public static readonly TimeSpan LongestStep = TimeSpan.FromMilliseconds(50);

    private TimeSpan? _lastUpdate;

    /// <summary>0 with the name hidden in the head, 1 with it all the way out.</summary>
    public float Progress { get; private set; }

    /// <summary>The whole logo as it rests once the name is out.</summary>
    public static Rectangle Bounds => Rectangle.Union(Head, Text);

    /// <summary>
    /// The head always answers the pointer; the name's area does too once any of the name shows,
    /// so moving from the head onto the name does not send it back in.
    /// </summary>
    public bool Contains(Point point) =>
        Head.Contains(point) || (Progress > 0 && Bounds.Contains(point));

    /// <summary>Moves the name out while the pointer is on the logo and back in otherwise.</summary>
    public void Update(Point? pointer, TimeSpan now)
    {
        var elapsed = _lastUpdate is { } last
            ? TimeSpan.FromTicks(Math.Clamp((now - last).Ticks, 0, LongestStep.Ticks))
            : TimeSpan.Zero;
        _lastUpdate = now;
        var step = (float)(elapsed / RevealTime);
        var hovered = pointer is { } point && Contains(point);
        Progress = Math.Clamp(Progress + (hovered ? step : -step), 0f, 1f);
    }

    /// <summary>Hides the name at once, for when the title screen is left and shown again.</summary>
    public void Reset()
    {
        Progress = 0;
        _lastUpdate = null;
    }

    /// <summary>
    /// The part of the name's bitmap that shows, where it is drawn and how opaque, or null while
    /// the name is wholly inside the head. The name eases out, fast at first and settling at the
    /// end, and fades in as it goes.
    /// </summary>
    public (Rectangle Source, Rectangle Destination, float Opacity)? TextFrame()
    {
        if (Progress <= 0) return null;
        var eased = 1 - MathF.Pow(1 - Progress, 3);
        var hiddenLeft = EmergeLine - Text.Width;
        var left = (int)MathF.Round(hiddenLeft + (Text.X - hiddenLeft) * eased);
        var visibleLeft = Math.Max(left, EmergeLine);
        var width = left + Text.Width - visibleLeft;
        if (width <= 0) return null;
        return (new Rectangle(visibleLeft - left, 0, width, Text.Height),
            new Rectangle(visibleLeft, Text.Y, width, Text.Height),
            0.25f + 0.75f * eased);
    }
}

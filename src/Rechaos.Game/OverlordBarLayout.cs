using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The Overlord bar above the city map and the sector view (SCR-UI-003, SCR-UI-004, FND-UI-017):
/// a portrait per seat 70 pixels apart, the viewed player's animated marker beside it and a
/// planning light under the marker.
/// </summary>
public static class OverlordBarLayout
{
    public const int SeatStride = 70;
    public const int EmptySeatFrameCount = 3;

    public static Rectangle Portrait(int seat) => new(18 + Stride(seat), 5, 32, 32);

    /// <summary>The 54-by-32 art a seat that is out of play shows over its portrait and marker.</summary>
    public static Rectangle EmptySeat(int seat) => new(18 + Stride(seat), 5, 54, 32);

    /// <summary>The black the bar fills beside each portrait before the marker is drawn there.</summary>
    public static Rectangle MarkerBackground(int seat) => new(50 + Stride(seat), 5, 20, 20);

    public static Rectangle Marker(int seat) => new(50 + Stride(seat), 6, 20, 20);

    public static Rectangle PlanningLight(int seat) => new(51 + Stride(seat), 30, 20, 6);

    /// <summary>FND-UI-015: the press area of a portrait on the sector view.</summary>
    public static Rectangle PortraitHit(int seat) => new(12 + Stride(seat), 5, 62, 32);

    public static Rectangle PlanningLightSource => new(66, 347, 20, 6);

    public static Rectangle EmptySeatSource(int frame)
    {
        if (frame is < 0 or >= EmptySeatFrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle(404 + frame * 27, 448, 54, 32);
    }

    /// <summary>
    /// The portrait row at source y 594 the sector view draws for a player with no gang there
    /// that the active player can see.
    /// </summary>
    public static Rectangle UnseenPortraitSource(int portraitId)
    {
        if (portraitId is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(portraitId));
        return new Rectangle(portraitId * 32, 594, 32, 32);
    }

    /// <summary>
    /// FND-UI-017: the light is lit while the seat is human (<c>players_human</c>) and its orders
    /// are not in. Only the network code marks a human seat's orders as in, so on a machine that
    /// seats every player the lights of the human seats stay lit.
    /// </summary>
    public static bool PlanningLightLit(bool human, bool ordersIn) => human && !ordersIn;

    private static int Stride(int seat)
    {
        if (seat is < 0 or >= MatchLimits.PlayerCount) throw new ArgumentOutOfRangeException(nameof(seat));
        return seat * SeatStride;
    }
}

/// <summary>
/// The pump's timer slot 1 steps the Overlord bar's marker and empty-seat art every 100 ms
/// (FND-UI-038, FND-TIMER-002).
/// </summary>
public static class ActivePlayerMarkerPresentation
{
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(100);

    public static int Frame(TimeSpan elapsed) =>
        (int)(Steps(elapsed) % OriginalSpriteLayout.ActivePlayerMarkerFrameCount);

    /// <summary>
    /// The marker frame at <paramref name="elapsed"/> when its counter was set back to 0 at
    /// <paramref name="reset"/>: it steps on the same ticks as before, counted from the reset.
    /// </summary>
    public static int Frame(TimeSpan elapsed, TimeSpan reset)
    {
        if (reset > elapsed) throw new ArgumentOutOfRangeException(nameof(reset));
        return (int)((Steps(elapsed) - Steps(reset)) % OriginalSpriteLayout.ActivePlayerMarkerFrameCount);
    }

    public static int EmptySeatFrame(TimeSpan elapsed) =>
        (int)(Steps(elapsed) % OverlordBarLayout.EmptySeatFrameCount);

    private static long Steps(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        return elapsed.Ticks / FrameDuration.Ticks;
    }
}

/// <summary>
/// The marker counter <c>0x00487B90</c>. It steps with the empty-seat counter on every tick of
/// timer slot 1, but the sector-view compositor sets it back to 0 each time it composes a sector
/// view (FND-UI-018, FND-UI-038). The rebuild draws every frame, so it counts a composition when
/// the sector view is entered, or shows another sector or another player's cards.
/// </summary>
public sealed class ActivePlayerMarkerClock
{
    private TimeSpan _start;
    private (int SectorId, PlayerId Viewed)? _composed;

    /// <summary>Notes a drawn sector view, restarting the marker when the view is a new composition.</summary>
    public void SectorView(int sectorId, PlayerId viewed, TimeSpan now)
    {
        if (_composed == (sectorId, viewed)) return;
        _composed = (sectorId, viewed);
        _start = now;
    }

    /// <summary>Notes a view that is not the sector view, so the next sector view restarts the marker.</summary>
    public void OtherView() => _composed = null;

    /// <summary>The marker frame at <paramref name="now"/>.</summary>
    public int Frame(TimeSpan now) =>
        ActivePlayerMarkerPresentation.Frame(now, now < _start ? now : _start);
}

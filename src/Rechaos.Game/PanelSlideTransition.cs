namespace Rechaos.Game;

public sealed class PanelSlideTransition
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(250);
    public const int StartOffset = 344;
    private ClientScreen? _screen;
    private TimeSpan _started;

    public void Begin(ClientScreen screen, TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = IsPanel(screen) ? screen : null;
        _started = now;
    }

    public void Begin(ClientScreen previous, ClientScreen current, TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = ShouldAnimate(previous, current) ? current : null;
        _started = now;
    }

    public int Offset(ClientScreen screen, TimeSpan now)
    {
        if (_screen != screen || now < _started) return 0;
        var progress = Math.Clamp((now - _started).TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        if (progress >= 1)
        {
            _screen = null;
            return 0;
        }
        return (int)Math.Round(StartOffset * (1 - progress));
    }

    public int Offset(ClientScreen screen, TimeSpan now, bool enabled)
    {
        if (enabled) return Offset(screen, now);
        Clear();
        return 0;
    }

    public void Clear() => _screen = null;

    public static bool ShouldAnimate(ClientScreen previous, ClientScreen current)
    {
        if (!IsPanel(current) || current == ClientScreen.Commands) return false;

        // Sector is a full underlying view. Animate only its forward entrance
        // from the city; returning from a nested detail panel must leave it fixed.
        if (current == ClientScreen.Sector) return previous == ClientScreen.City;

        return true;
    }

    public static bool IsPanel(ClientScreen screen) => screen is
        ClientScreen.Options or ClientScreen.Help or ClientScreen.GameInfo or ClientScreen.Commands
        or ClientScreen.Hire or ClientScreen.Events or ClientScreen.ComlinkView
        or ClientScreen.ComlinkSend or ClientScreen.Sector or ClientScreen.SectorGangs
        or ClientScreen.Gang or ClientScreen.Site or ClientScreen.ItemInformation
        or ClientScreen.Finance or ClientScreen.Ranking or ClientScreen.Items
        or ClientScreen.Give or ClientScreen.GiveTarget or ClientScreen.Sell
        or ClientScreen.CombatSummary or ClientScreen.Search;
}

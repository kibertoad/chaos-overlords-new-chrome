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

    public void Clear() => _screen = null;

    public static bool IsPanel(ClientScreen screen) => screen is
        ClientScreen.Options or ClientScreen.Help or ClientScreen.Commands
        or ClientScreen.Hire or ClientScreen.Events or ClientScreen.Sector
        or ClientScreen.Gang or ClientScreen.Site or ClientScreen.ItemInformation
        or ClientScreen.Finance or ClientScreen.Ranking or ClientScreen.Items
        or ClientScreen.Give or ClientScreen.CombatSummary or ClientScreen.Search;
}

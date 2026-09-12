using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Inset faces used only to render setup selections. The original click regions
/// include the raised frame and section-label overlap, so they must not be used
/// as the visible highlight rectangles.
/// </summary>
public static class SetupSelectionLayout
{
    private static readonly int[] ScenarioRows = [109, 144, 179, 215, 250];
    private static readonly int[] OptionRows = [337, 364, 391, 418];

    public static Rectangle Scenario(int scenario)
    {
        if (scenario is < 0 or >= 10) throw new ArgumentOutOfRangeException(nameof(scenario));
        return new Rectangle(80 + scenario % 2 * 112, ScenarioRows[scenario / 2], 108, 31);
    }

    public static Rectangle Duration(int duration)
    {
        if (duration is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(duration));
        return new Rectangle(80 + duration * 56, 285, duration == 3 ? 52 : 50, 23);
    }

    public static Rectangle AiMentality(int mentality)
    {
        if (mentality is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(mentality));
        return new Rectangle(80, OptionRows[mentality], 108, 23);
    }

    public static Rectangle PlanningTime(int limit)
    {
        if (limit is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(limit));
        return new Rectangle(192, OptionRows[limit], 108, 23);
    }

}

public static class OriginalSelectionLightLayout
{
    private static readonly int[] ScenarioRows = [112, 147, 182, 218, 253];
    private static readonly int[] OptionRows = [340, 367, 394, 421];

    public static Rectangle Scenario(int button)
    {
        if (button is < 0 or >= 10) throw new ArgumentOutOfRangeException(nameof(button));
        return new Rectangle(183 + button % 2 * 112, ScenarioRows[button / 2], 3, 11);
    }

    public static Rectangle Duration(int duration)
    {
        if (duration is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(duration));
        return new Rectangle(126 + duration * 57, 288, 3, 11);
    }

    public static Rectangle AiMentality(int mentality)
    {
        if (mentality is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(mentality));
        return new Rectangle(183, OptionRows[mentality], 3, 11);
    }

    public static Rectangle PlanningTime(int limit)
    {
        if (limit is < 0 or >= 4) throw new ArgumentOutOfRangeException(nameof(limit));
        return new Rectangle(297, OptionRows[limit], 3, 11);
    }

    public static Rectangle EndgameTab(Rectangle tab) =>
        new(tab.Right - 5, tab.Y + 5, 3, 11);

    public static Rectangle CityEvents => new(541, 129, 3, 11);

    public static Rectangle CityComlinkView => new(593, 129, 3, 11);
}

public static class ActivePlayerMarkerPresentation
{
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(80);

    public static int Frame(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        return (int)(elapsed.TotalMilliseconds / FrameDuration.TotalMilliseconds)
            % OriginalSpriteLayout.ActivePlayerMarkerFrameCount;
    }
}

using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public enum SetupPanelControlKind
{
    Scenario,
    Duration,
    AiMentality,
    PlanningTime
}

/// <summary>A left-panel control of the setup screen, held from press to release.</summary>
public readonly record struct SetupPanelControl(SetupPanelControlKind Kind, int Index);

/// <summary>
/// SCR-SETUP-001, FND-SETUP-013: the left panel's controls on the 640x460 surface and their pressed
/// images in the setup controls sheet. <see cref="Rectangle.Contains(Point)"/> leaves the right and
/// bottom edges out, as <c>PtInRect</c> does.
/// </summary>
public static class SetupPanelLayout
{
    public const int ScenarioCount = 10;
    public const int OptionCount = 4;

    public static IReadOnlyList<Rectangle> Scenarios { get; } =
    [
        .. Enumerable.Range(0, ScenarioCount).Select(button => new Rectangle(
            80 + 114 * (button % 2), (button < 6 ? 109 : 110) + 35 * (button / 2), 110, 32))
    ];

    public static IReadOnlyList<Rectangle> Durations { get; } =
        [.. Enumerable.Range(0, OptionCount).Select(index => new Rectangle(80 + 57 * index, 285, 53, 23))];

    /// <summary>Where a press is refused while the scenario has no time limit.</summary>
    public static Rectangle DurationArea => new(80, 284, 224, 24);

    public static IReadOnlyList<Rectangle> AiMentalities { get; } =
        [.. Enumerable.Range(0, OptionCount).Select(row => new Rectangle(80, 337 + 27 * row, 110, 24))];

    public static IReadOnlyList<Rectangle> PlanningTimes { get; } =
        [.. Enumerable.Range(0, OptionCount).Select(row => new Rectangle(194, 337 + 27 * row, 110, 24))];

    /// <summary>The 8x16 selection light of the setup controls sheet.</summary>
    public static Rectangle LightSource => new(304, 138, 8, 16);

    /// <summary>Tested in the original's order: scenario, time limit, left column, right column.</summary>
    public static SetupPanelControl? HitTest(Point point, bool timed)
    {
        var scenario = IndexAt(Scenarios, point);
        if (scenario >= 0) return new(SetupPanelControlKind.Scenario, scenario);
        var duration = timed ? IndexAt(Durations, point) : -1;
        if (duration >= 0) return new(SetupPanelControlKind.Duration, duration);
        var mentality = IndexAt(AiMentalities, point);
        if (mentality >= 0) return new(SetupPanelControlKind.AiMentality, mentality);
        var planningTime = IndexAt(PlanningTimes, point);
        return planningTime >= 0 ? new(SetupPanelControlKind.PlanningTime, planningTime) : null;
    }

    public static Rectangle Destination(SetupPanelControl control) => control.Kind switch
    {
        SetupPanelControlKind.Scenario => Scenarios[control.Index],
        SetupPanelControlKind.Duration => Durations[control.Index],
        SetupPanelControlKind.AiMentality => AiMentalities[control.Index],
        SetupPanelControlKind.PlanningTime => PlanningTimes[control.Index],
        _ => throw new ArgumentOutOfRangeException(nameof(control))
    };

    public static Rectangle PressedSource(SetupPanelControl control) => control.Kind switch
    {
        SetupPanelControlKind.Scenario =>
            new(110 * (control.Index % 2), 32 * (control.Index / 2), 110, 32),
        SetupPanelControlKind.Duration => new(53 * control.Index, 256, 53, 24),
        SetupPanelControlKind.AiMentality => new(0, 160 + 24 * control.Index, 110, 24),
        SetupPanelControlKind.PlanningTime => new(110, 160 + 24 * control.Index, 110, 24),
        _ => throw new ArgumentOutOfRangeException(nameof(control))
    };

    private static int IndexAt(IReadOnlyList<Rectangle> rectangles, Point point)
    {
        for (var index = 0; index < rectangles.Count; index++)
            if (rectangles[index].Contains(point)) return index;
        return -1;
    }
}

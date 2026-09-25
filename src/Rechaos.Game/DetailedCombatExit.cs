using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>What one pointer update does to the Detailed Combat presentation.</summary>
public enum DetailedCombatPointerResult
{
    None,
    Rejected,
    EndPresentation
}

/// <summary>
/// The left button during Detailed Combat (SCR-COMBAT-002, FND-COMBAT-010): a press on the Exit
/// face is tracked, the face is drawn pressed while the pointer stays over it, and a release over
/// it ends the whole presentation. A press outside the panel plays the rejected sound, and any
/// other press inside the panel does nothing.
/// </summary>
public sealed class DetailedCombatExit
{
    /// <summary>Whether a press that started on the Exit face is still held.</summary>
    public bool Tracking { get; private set; }

    /// <summary>Whether the Exit face is drawn pressed.</summary>
    public bool ShowsPressed { get; private set; }

    /// <param name="point">The pointer in virtual coordinates, or null when it is off the game.</param>
    /// <param name="down">Whether the left button is down now.</param>
    /// <param name="wasDown">Whether it was down on the previous update.</param>
    public DetailedCombatPointerResult Update(Point? point, bool down, bool wasDown)
    {
        var over = point is { } at && CombatPanelLayout.Exit.Contains(at);
        if (down && !wasDown)
        {
            if (over)
            {
                Tracking = true;
                ShowsPressed = true;
                return DetailedCombatPointerResult.None;
            }
            return point is { } pressed && !CombatPanelLayout.Panel.Contains(pressed)
                ? DetailedCombatPointerResult.Rejected
                : DetailedCombatPointerResult.None;
        }
        if (!Tracking) return DetailedCombatPointerResult.None;
        if (down)
        {
            ShowsPressed = over;
            return DetailedCombatPointerResult.None;
        }
        Reset();
        return over ? DetailedCombatPointerResult.EndPresentation : DetailedCombatPointerResult.None;
    }

    public void Reset()
    {
        Tracking = false;
        ShowsPressed = false;
    }
}

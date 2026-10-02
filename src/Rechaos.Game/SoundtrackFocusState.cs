namespace Rechaos.Game;

/// <summary>RULE-AUDIO-002, FND-AUDIO-016: the game's inactive flag changes only
/// when the full event handler runs, not during the window-only fade pump.</summary>
public sealed class SoundtrackFocusState
{
    public bool WindowActive { get; private set; } = true;

    public bool Activate(bool suppressGameEvents) => Apply(true, suppressGameEvents);
    public bool Deactivate(bool suppressGameEvents) => Apply(false, suppressGameEvents);

    private bool Apply(bool active, bool suppressGameEvents)
    {
        if (suppressGameEvents) return false;
        WindowActive = active;
        return true;
    }
}

using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackFocusStateTests
{
    [Fact]
    public void FadeDispatchDoesNotRunFocusActionsOrChangeGameInactiveFlag()
    {
        // FND-AUDIO-016, RULE-AUDIO-002: a dispatched focus message during
        // the fade is consumed without entering the game's pause/resume handler.
        var focus = new SoundtrackFocusState();
        Assert.False(focus.Deactivate(suppressGameEvents: true));
        Assert.True(focus.WindowActive);
        Assert.True(focus.Deactivate(suppressGameEvents: false));
        Assert.False(focus.WindowActive);
        Assert.False(focus.Activate(suppressGameEvents: true));
        Assert.False(focus.WindowActive);
        Assert.True(focus.Activate(suppressGameEvents: false));
        Assert.True(focus.WindowActive);
    }
}

using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackFocusHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GameHandlersConsumeFocusChangesDuringFade(bool initiallyActive)
    {
        // FND-AUDIO-016, RULE-AUDIO-002: window-only dispatch must not enter
        // the game's music pause/resume or level-application branches.
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        var focus = new SoundtrackFocusState();
        if (!initiallyActive) focus.Deactivate(false);
        Set(game, "_soundtrackFocus", focus);
        Set(game, "_soundtrackEnabled", true);
        Set(game, "_soundtrackFade", new SoundtrackFade(0.5f, TimeSpan.Zero));
        Invoke(game, "OnDeactivated");
        Assert.Equal(initiallyActive, focus.WindowActive);
        Invoke(game, "OnActivated");
        Assert.Equal(initiallyActive, focus.WindowActive);
        Assert.True((bool)Field("_soundtrackEnabled").GetValue(game)!);
        Assert.False((bool)Field("_soundtrackAwaitingStart").GetValue(game)!);

        // A later event after the fade is handled normally, even with music off.
        Set(game, "_soundtrackFade", null);
        Set(game, "_soundtrackEnabled", false);
        Invoke(game, "OnDeactivated");
        Assert.False(focus.WindowActive);
        Invoke(game, "OnActivated");
        Assert.True(focus.WindowActive);
    }

    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
    private static void Set(ChaosGame game, string name, object? value) => Field(name).SetValue(game, value);
    private static void Invoke(ChaosGame game, string name) =>
        (typeof(ChaosGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(name)).Invoke(game, [game, EventArgs.Empty]);
}

using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireRejectPressTests
{
    [Fact]
    public void AnEndedTurnLetsGoOfAHeldRejectCross()
    {
        // SCR-HIRE-002: the cross acts on a release inside it. A cross still held when the turn
        // ends is let go with the held offer, so its release cannot snub the offer the next
        // planning turn puts in that slot.
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        var pressed = Field("_pressedHireRejectSlot");
        pressed.SetValue(game, 0);

        (typeof(ChaosGame).GetMethod("ForgetHireDrag", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(ChaosGame), "ForgetHireDrag")).Invoke(game, null);

        Assert.Null(pressed.GetValue(game));
    }

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ChaosGame), name);
}

using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// RULE-TIMER-002: the original tests the time limit only on a pass of the planning loop, so a
// panel open or an offer or control held at the limit keeps the turn going until it is closed or
// let go.
public sealed class PlanningTimerLoopTests
{
    [Fact]
    public void AnOpenPanelOrAHeldOfferDefersTheExpiry()
    {
        var state = OriginalNewGameExperimentTests.ReplayedMatch("EXP-SETUP-001", 0);
        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Field("_screens").SetValue(game, new ScreenRouter());
        Field("_planningTimer").SetValue(game, new PlanningTimer());
        Field("_state").SetValue(game, state);
        var router = (ScreenRouter)Field("_screens").GetValue(game)!;
        var timer = (PlanningTimer)Field("_planningTimer").GetValue(game)!;
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        var pastLimit = TimeSpan.FromSeconds(31);

        router.Show(ClientScreen.Commands);
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);
        Assert.True(timer.HasExpired(pastLimit));
        Assert.False(AtPlanningLoopPass(game));

        router.Show(ClientScreen.City);
        Field("_draggedHireDefinitionId").SetValue(game, (short)1);
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);
        Assert.False(AtPlanningLoopPass(game));

        Field("_draggedHireDefinitionId").SetValue(game, null);
        // FND-HIRE-008, FND-UI-032: the reject cross and a console tile are held in their own
        // loops until the button is released.
        Field("_pressedHireRejectSlot").SetValue(game, 0);
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);
        Field("_pressedHireRejectSlot").SetValue(game, null);
        Field("_pressedCityConsoleControl").SetValue(game, CityConsoleControl.Done);
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);
        Field("_pressedCityConsoleControl").SetValue(game, null);
        Assert.True(AtPlanningLoopPass(game));
        router.Show(ClientScreen.Sector);
        Assert.True(AtPlanningLoopPass(game));
    }

    [Fact]
    public void AGangHeldOnTheSectorViewDefersTheExpiryUntilItIsLetGo()
    {
        // FND-UI-044: a left press on the portrait of one of the player's cards waits in the
        // individual command handler for the pointer to move two pixels or the button to come up,
        // then follows the dragged gang until the button comes up, pumping window messages only.
        // The planning loop's expiry test runs again after the handler returns.
        var state = OriginalNewGameExperimentTests.ReplayedMatch("EXP-SETUP-001", 0);
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Field("_screens").SetValue(game, new ScreenRouter());
        Field("_planningTimer").SetValue(game, new PlanningTimer());
        Field("_state").SetValue(game, state);
        var router = (ScreenRouter)Field("_screens").GetValue(game)!;
        var timer = (PlanningTimer)Field("_planningTimer").GetValue(game)!;
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        var pastLimit = TimeSpan.FromSeconds(31);
        router.Show(ClientScreen.Sector);

        // Pressed, not yet moved: the handler's first wait loop.
        Field("_draggedGangId").SetValue(game, new GangId(0));
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);
        Assert.True(timer.HasExpired(pastLimit));
        // Moved past the press point: the drag loop.
        Field("_gangDragStarted").SetValue(game, true);
        Assert.False(UpdatePlanningTimer(game, pastLimit));
        Assert.True(timer.IsActive);

        Field("_gangDragStarted").SetValue(game, false);
        Field("_draggedGangId").SetValue(game, null);
        Assert.True(AtPlanningLoopPass(game));
    }

    private static bool UpdatePlanningTimer(ChaosGame game, TimeSpan now) =>
        (bool)Method("UpdatePlanningTimer").Invoke(game, [now])!;

    private static bool AtPlanningLoopPass(ChaosGame game) =>
        (bool)Method("AtPlanningLoopPass").Invoke(game, null)!;

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ChaosGame), name);

    private static MethodInfo Method(string name) => typeof(ChaosGame)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(ChaosGame), name);
}

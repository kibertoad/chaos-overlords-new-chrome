using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// RULE-TIMER-002: the original tests the time limit only on a pass of the planning loop, so a
// panel open or an offer held at the limit keeps the turn going until it is closed or let go.
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
        Assert.True(AtPlanningLoopPass(game));
        router.Show(ClientScreen.Sector);
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

using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativeFinalViewHandlerTests
{
    [Fact]
    public void ActiveFirstPlanningDateMatchesOriginalEntry()
    {
        // EXP-SETUP-001, FND-UI-040: active planning still begins at week 1,
        // with no previous-turn date available to print in Events.
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var replayType = typeof(OriginalNewGameExperimentTests);
        var recorded = replayType.GetMethod("Run", flags)!.Invoke(null, ["EXP-SETUP-001", 0]);
        object?[] replayArguments = [recorded, 0];
        var state = (MatchState)replayType.GetMethod("StartMatch", flags)!.Invoke(null, replayArguments)!;
        Assert.Null(state.Outcome);
        Assert.True(LocalPlanningLight(state, state.Players[0]));
        var elapsed = MatchCalendar.PresentationElapsedTurns(state);
        Assert.Equal(0, elapsed);
        Assert.Equal((2050, 1), MatchCalendar.Of(elapsed));
        Assert.Null(LastTurnEventsLayout.Date(elapsed));
        // FND-UI-043: earlier local seats are dark while later humans still wait.
        state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
        state.Coordinator.FinishCommand(new PlayerId(0));
        Assert.False(LocalPlanningLight(state, state.Players[0]));
        Assert.True(LocalPlanningLight(state, state.Players[2]));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false, "EXP-TURN-042")]
    public void ReadyAndDoneVisitFinalViewersWithoutResolvingAnotherTurn(bool multipleHumans, bool syntheticReport,
        string experiment = "EXP-TURN-041")
    {
        // FND-OBJECTIVE-004, FND-STATE-010, EXP-TURN-041, EXP-TURN-042: final views precede awards.
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var replayType = typeof(OriginalNewGameExperimentTests);
        var recorded = replayType.GetMethod("Run", flags)!.Invoke(null, [experiment, 0]);
        object?[] replayArguments = [recorded, 0];
        var state = (MatchState)replayType.GetMethod("StartMatch", flags)!.Invoke(null, replayArguments)!;
        Assert.NotNull(state.Outcome);
        // FND-UI-043: completed local planning leaves the human light dark.
        Assert.False(LocalPlanningLight(state, state.Players[0]));
        // FND-UI-041, EXP-TURN-042: original final city is week 26; its
        // last-turn report is week 25, before the elapsed counter increments.
        Assert.Equal(27, state.Coordinator.Turn);
        var elapsed = MatchCalendar.PresentationElapsedTurns(state);
        Assert.Equal(25, elapsed);
        Assert.Equal((2050, 26), MatchCalendar.Of(elapsed));
        Assert.Equal(("2050", "25"), LastTurnEventsLayout.Date(elapsed));
        if (syntheticReport)
        {
            // Synthetic report exercises dismissal; EXP-TURN-041 itself has no
            // reviewable reports for the first viewer at this endpoint.
            var queue = (NotificationQueue)typeof(MatchState).GetMethod("GetNotificationQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, [new PlayerId(0)])!;
            queue.Enqueue(new GameNotification(long.MaxValue, state.Outcome.Turn,
                TurnPhase.Execution, ExecutionPhase.Chaos, GameNotificationKind.Crackdown, SectorId: 0));
        }
        if (multipleHumans)
        {
            state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
            state.Players[2].Status = PlayerStatus.Active;
            // FND-UI-043: every local human completed the last round, so the second light is dark too.
            Assert.False(LocalPlanningLight(state, state.Players[2]));
        }
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        foreach (var name in new[] { "_pendingFinalViews", "_presentedHotSeatEliminations",
                     "_gangSelection", "_screens", "_lastTurnReportCache", "_combatResultCache",
                     "_eventViewedPages", "_lastTurnEventArchive", "_planningTimer" })
        {
            var field = Field(name);
            field.SetValue(game, Activator.CreateInstance(field.FieldType));
        }
        Field("_state").SetValue(game, state);
        var router = (ScreenRouter)Field("_screens").GetValue(game)!;
        var randomState = state.Random.State;
        var consumption = state.Random.ConsumptionCount;
        var turn = state.Coordinator.Turn;
        Call(game, "BeginFinalViews");
        Assert.Equal(new PlayerId(0), Field("_finalViewPlayer").GetValue(game));
        if (multipleHumans)
        {
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
        }
        Assert.Equal(syntheticReport || experiment == "EXP-TURN-042" ? ClientScreen.Events : ClientScreen.City,
            router.Current);
        CloseReports(game, router);
        Call(game, "AdvanceTurn");
        if (multipleHumans)
        {
            Assert.Equal(new PlayerId(2), Field("_finalViewPlayer").GetValue(game));
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
            CloseReports(game, router);
            Call(game, "AdvanceTurn");
        }
        Assert.Null(Field("_finalViewPlayer").GetValue(game));
        Assert.Equal(ClientScreen.Endgame, router.Current);
        Assert.Equal(turn, state.Coordinator.Turn);
        Assert.Equal(randomState, state.Random.State);
        Assert.Equal(consumption, state.Random.ConsumptionCount);
        Assert.False((bool)Field("_idleGangWarningOpen").GetValue(game)!);
    }

    private static bool LocalPlanningLight(MatchState state, MatchPlayerState player)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        return (bool)typeof(ChaosGame).GetMethod("PlanningLightLit",
            BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, [state, player])!;
    }

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Call(ChaosGame game, string name) => typeof(ChaosGame)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);

    private static void CloseReports(ChaosGame game, ScreenRouter router)
    {
        // SCR-UI-003, FND-STATE-010: reports return to the completed city;
        // no planning timer is started and Done remains a separate action.
        var viewer = Field("_finalViewPlayer").GetValue(game);
        if (router.Current == ClientScreen.CombatSummary)
            Call(game, "CloseCombatResults");
        if (router.Current == ClientScreen.Events)
            Call(game, "CloseEvents");
        Assert.Equal(ClientScreen.City, router.Current);
        Assert.Equal(viewer, Field("_finalViewPlayer").GetValue(game));
        Assert.False(((PlanningTimer)Field("_planningTimer").GetValue(game)!).IsActive);
    }
}

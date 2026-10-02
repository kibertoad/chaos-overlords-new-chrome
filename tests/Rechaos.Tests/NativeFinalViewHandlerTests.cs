using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativeFinalViewHandlerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ReadyAndDoneVisitFinalViewersWithoutResolvingAnotherTurn(bool multipleHumans, bool syntheticReport)
    {
        // FND-OBJECTIVE-004, FND-STATE-010, EXP-TURN-041: final views precede awards.
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var replayType = typeof(OriginalNewGameExperimentTests);
        var recorded = replayType.GetMethod("Run", flags)!.Invoke(null, ["EXP-TURN-041", 0]);
        object?[] replayArguments = [recorded, 0];
        var state = (MatchState)replayType.GetMethod("StartMatch", flags)!.Invoke(null, replayArguments)!;
        Assert.NotNull(state.Outcome);
        if (multipleHumans)
        {
            state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
            state.Players[2].Status = PlayerStatus.Active;
        }
        if (syntheticReport)
        {
            // Synthetic reports exercise dismissal; EXP-TURN-041 itself has no
            // reviewable reports for the first viewer at this endpoint.
            AddFinalTurnReport(state, new PlayerId(0));
            if (multipleHumans) AddFinalTurnReport(state, new PlayerId(2));
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
        // A timed limit, so a planning clock armed in the final view would show as active.
        Field("_selectedPlanningTimeLimit").SetValue(game, PlanningTimeLimit.TwoMinutes);
        // The state was changed outside a recorder above, so the journal does not verify it.
        Field("_actions").SetValue(game, new MatchActions(MatchReplayRecorder.Unverified(state)));
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
        Assert.Equal(syntheticReport ? ClientScreen.Events : ClientScreen.City, router.Current);
        CloseReports(game, router, state);
        Call(game, "AdvanceTurn");
        if (multipleHumans)
        {
            Assert.Equal(new PlayerId(2), Field("_finalViewPlayer").GetValue(game));
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
            if (syntheticReport) Assert.Equal(ClientScreen.Events, router.Current);
            CloseReports(game, router, state);
            Call(game, "AdvanceTurn");
        }
        Assert.Null(Field("_finalViewPlayer").GetValue(game));
        Assert.Equal(ClientScreen.Endgame, router.Current);
        Assert.Equal(turn, state.Coordinator.Turn);
        Assert.Equal(randomState, state.Random.State);
        Assert.Equal(consumption, state.Random.ConsumptionCount);
        Assert.False((bool)Field("_idleGangWarningOpen").GetValue(game)!);
    }

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Call(ChaosGame game, string name) => typeof(ChaosGame)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);

    private static void CloseReports(ChaosGame game, ScreenRouter router, MatchState state)
    {
        // SCR-UI-003, FND-STATE-010: reports return to the completed city;
        // no planning timer is started and Done remains a separate action.
        var viewer = (PlayerId)Field("_finalViewPlayer").GetValue(game)!;
        var reports = LastTurnEventProjection.For(state, viewer);
        // The ordinary planning entry sets this flag, and closing the reports then arms the clock.
        Assert.False((bool)Field("_deferComlinkAlertUntilPlanningVisible").GetValue(game)!);
        if (router.Current == ClientScreen.CombatSummary)
            Call(game, "CloseCombatResults");
        if (router.Current == ClientScreen.Events)
            Call(game, "CloseEvents");
        Assert.Equal(ClientScreen.City, router.Current);
        Assert.Equal(viewer, Field("_finalViewPlayer").GetValue(game));
        Assert.False(((PlanningTimer)Field("_planningTimer").GetValue(game)!).IsActive);
        // Closing the review dismisses the reports and keeps them for the Events button.
        Assert.Empty(LastTurnEventProjection.For(state, viewer));
        var archive = (LastTurnEventArchive)Field("_lastTurnEventArchive").GetValue(game)!;
        Assert.Equal(reports, archive.For(viewer, state.Coordinator.Turn));
    }

    private static void AddFinalTurnReport(MatchState state, PlayerId player)
    {
        // QueueNotification stamps the current turn, and a last-turn report belongs to the
        // completed one, so the report is built here with the player's next sequence number.
        var instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var queue = (NotificationQueue)typeof(MatchState).GetMethod("GetNotificationQueue", instance)!
            .Invoke(state, [player])!;
        var sequences = (Dictionary<PlayerId, long>)typeof(MatchState)
            .GetField("_nextNotificationSequences", instance)!.GetValue(state)!;
        queue.Enqueue(new GameNotification(sequences[player]++, state.Outcome!.Turn,
            TurnPhase.Execution, ExecutionPhase.Chaos, GameNotificationKind.Crackdown, SectorId: 0));
    }
}

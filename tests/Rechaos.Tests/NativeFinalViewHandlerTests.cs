using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativeFinalViewHandlerTests
{
    [Fact]
    public void ActiveFirstPlanningDateMatchesOriginalEntry()
    {
        // EXP-SETUP-001, FND-UI-040, SCR-EVENT-001: active planning still begins at week 1,
        // with no previous-turn date available to print in Events.
        var state = OriginalNewGameExperimentTests.ReplayedMatch("EXP-SETUP-001", 0);
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
        // FND-TURN-006, FND-UI-043: the next turn's upkeep runs before the reset, which
        // relights both seats for the next planning round.
        while (state.Coordinator.Phase != TurnPhase.Upkeep)
        {
            _ = state.Coordinator.Phase switch
            {
                TurnPhase.Command => state.FinishCommand(state.Coordinator.ActivePlayer!.Value),
                TurnPhase.Execution => state.FinishExecutionPhase(),
                TurnPhase.Hire => state.FinishHire(state.Coordinator.ActivePlayer!.Value),
                _ => state.FinishPlayerElimination(),
            };
        }
        Assert.Equal(2, state.Coordinator.Turn);
        Assert.False(LocalPlanningLight(state, state.Players[0]));
        Assert.False(LocalPlanningLight(state, state.Players[2]));
        state.FinishUpkeep();
        Assert.True(LocalPlanningLight(state, state.Players[0]));
        Assert.True(LocalPlanningLight(state, state.Players[2]));
    }

    [Theory]
    [InlineData("EXP-TURN-041")]
    [InlineData("EXP-TURN-042")]
    public void CompletedMatchDatesPrecedeTheElapsedTurnIncrement(string experiment)
    {
        // FND-UI-041, EXP-TURN-042, SCR-EVENT-001: original final city is week 26; its
        // last-turn report is week 25, before the elapsed counter increments.
        var state = OriginalNewGameExperimentTests.ReplayedMatch(experiment, 0);
        Assert.NotNull(state.Outcome);
        Assert.Equal(27, state.Coordinator.Turn);
        var elapsed = MatchCalendar.PresentationElapsedTurns(state);
        Assert.Equal(25, elapsed);
        Assert.Equal((2050, 26), MatchCalendar.Of(elapsed));
        Assert.Equal(("2050", "25"), LastTurnEventsLayout.Date(elapsed));
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
        var state = OriginalNewGameExperimentTests.ReplayedMatch(experiment, 0);
        Assert.NotNull(state.Outcome);
        // FND-UI-043: completed local planning leaves the human light dark.
        Assert.False(LocalPlanningLight(state, state.Players[0]));
        if (multipleHumans)
        {
            state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
            state.Players[2].Status = PlayerStatus.Active;
            // FND-UI-043: every local human completed the last round, so the second light is dark too.
            Assert.False(LocalPlanningLight(state, state.Players[2]));
        }
        if (syntheticReport)
        {
            // Synthetic reports exercise dismissal; EXP-TURN-041 itself has no
            // reviewable reports for the first viewer at this endpoint, and the
            // original enters that final city with no report panel open (FND-UI-042).
            AddFinalTurnReport(state, new PlayerId(0));
            if (multipleHumans) AddFinalTurnReport(state, new PlayerId(2));
        }
        var (game, router) = FinalViewGame(state);
        var randomState = state.Random.State;
        var consumption = state.Random.ConsumptionCount;
        var turn = state.Coordinator.Turn;
        Call(game, "BeginFinalViews");
        // FND-UI-041: the original's final city view is drawn for player 0.
        AssertViewer(game, new PlayerId(0));
        if (multipleHumans)
        {
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
        }
        Assert.Equal(syntheticReport || experiment == "EXP-TURN-042" ? ClientScreen.Events : ClientScreen.City,
            router.Current);
        CloseReports(game, router, state);
        Call(game, "AdvanceTurn");
        if (multipleHumans)
        {
            AssertViewer(game, new PlayerId(2));
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
            AssertViewer(game, new PlayerId(2));
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

    private static bool LocalPlanningLight(MatchState state, MatchPlayerState player)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        var method = typeof(ChaosGame).GetMethod("PlanningLightLit", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(ChaosGame), "PlanningLightLit");
        return (bool)method.Invoke(game, [state, player])!;
    }

    [Fact]
    public void AHumanEliminatedOnTheLastTurnGetsItsCardBeforeTheAwards()
    {
        // FND-OBJECTIVE-004: a local human the last turn eliminated is visited in slot order
        // with its elimination card in place of a final view, and the awards follow it.
        var state = OriginalNewGameExperimentTests.ReplayedMatch("EXP-TURN-041", 0);
        Assert.NotNull(state.Outcome);
        state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
        state.Players[2].Status = PlayerStatus.Eliminated;
        var (game, router) = FinalViewGame(state);
        var randomState = state.Random.State;
        var turn = state.Coordinator.Turn;
        Call(game, "BeginFinalViews");
        AssertViewer(game, new PlayerId(0));
        if (router.Current == ClientScreen.Handoff)
            Call(game, "FinishHandoff");
        Assert.Equal(ClientScreen.City, router.Current);
        Call(game, "AdvanceTurn");
        Assert.Equal(new PlayerId(2), Field("_eliminationHandoffPlayer").GetValue(game));
        if (router.Current == ClientScreen.Handoff)
            Call(game, "FinishHandoff");
        Assert.Equal(ClientScreen.Elimination, router.Current);
        Assert.Contains(new PlayerId(2), (HashSet<PlayerId>)Field("_presentedHotSeatEliminations").GetValue(game)!);
        Call(game, "FinishHotSeatEliminationPresentation");
        Assert.Null(Field("_finalViewPlayer").GetValue(game));
        Assert.Null(Field("_eliminationHandoffPlayer").GetValue(game));
        Assert.Equal(ClientScreen.Endgame, router.Current);
        Assert.Equal(turn, state.Coordinator.Turn);
        Assert.Equal(randomState, state.Random.State);
    }

    private static (ChaosGame Game, ScreenRouter Router) FinalViewGame(MatchState state)
    {
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
        return (game, (ScreenRouter)Field("_screens").GetValue(game)!);
    }

    /// <summary>The player the city screens are drawn for and act as, and the final-view seat.</summary>
    private static void AssertViewer(ChaosGame game, PlayerId expected)
    {
        Assert.Equal(expected, Field("_finalViewPlayer").GetValue(game));
        var viewer = typeof(ChaosGame).GetProperty("PlanningViewer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(nameof(ChaosGame), "PlanningViewer");
        Assert.Equal(expected, viewer.GetValue(game));
    }

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ChaosGame), name);
    private static void Call(ChaosGame game, string name) => (typeof(ChaosGame)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(ChaosGame), name)).Invoke(game, null);

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

using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativeFinalViewHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadyAndDoneVisitFinalViewersWithoutResolvingAnotherTurn(bool multipleHumans)
    {
        // FND-OBJECTIVE-004, FND-STATE-010, EXP-TURN-041: final views precede awards.
        var state = OriginalNewGameExperimentTests.ReplayedMatch("EXP-TURN-041", 0);
        Assert.NotNull(state.Outcome);
        if (multipleHumans)
        {
            state.Players[2].Setup = state.Players[2].Setup with { Controller = PlayerController.Human };
            state.Players[2].Status = PlayerStatus.Active;
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
        Assert.Equal(ClientScreen.City, router.Current);
        Call(game, "AdvanceTurn");
        if (multipleHumans)
        {
            AssertViewer(game, new PlayerId(2));
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
            AssertViewer(game, new PlayerId(2));
            Assert.Equal(ClientScreen.City, router.Current);
            Call(game, "AdvanceTurn");
        }
        Assert.Null(Field("_finalViewPlayer").GetValue(game));
        Assert.Equal(ClientScreen.Endgame, router.Current);
        Assert.Equal(turn, state.Coordinator.Turn);
        Assert.Equal(randomState, state.Random.State);
        Assert.Equal(consumption, state.Random.ConsumptionCount);
        Assert.False((bool)Field("_idleGangWarningOpen").GetValue(game)!);
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
                     "_gangSelection", "_screens", "_lastTurnReportCache", "_combatResultCache" })
        {
            var field = Field(name);
            field.SetValue(game, Activator.CreateInstance(field.FieldType));
        }
        Field("_state").SetValue(game, state);
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
}

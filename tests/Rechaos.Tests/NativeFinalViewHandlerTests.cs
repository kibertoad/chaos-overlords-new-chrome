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
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        foreach (var name in new[] { "_pendingFinalViews", "_presentedHotSeatEliminations",
                     "_gangSelection", "_screens", "_lastTurnReportCache", "_combatResultCache" })
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
        Assert.Contains(router.Current, new[] { ClientScreen.City, ClientScreen.Events, ClientScreen.CombatSummary });
        Call(game, "AdvanceTurn");
        if (multipleHumans)
        {
            Assert.Equal(new PlayerId(2), Field("_finalViewPlayer").GetValue(game));
            Assert.Equal(ClientScreen.Handoff, router.Current);
            Call(game, "FinishHandoff");
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
}

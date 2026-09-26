using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The footer's warning that the turn on screen has orders the server has not taken yet.
/// </summary>
/// <remarks>
/// It used to be a line in the shared message slot, where the reconnect modal closing, or anything
/// else said in the meantime, took it away while the draft was still unsaved.
/// </remarks>
public sealed class DelayedDraftStatusTests
{
    [Fact]
    public void TheWarningFollowsTheTurnOnScreen()
    {
        var state = new MultiplayerUiState { PlanningTurn = 4 };
        Assert.False(state.OpenTurnDraftUnsaved);

        state.DelayedDraftTurn = 4;
        Assert.True(state.OpenTurnDraftUnsaved);

        // The session stops retrying a draft once its turn seals, so the next turn is not warned about.
        state.PlanningTurn = 5;
        Assert.False(state.OpenTurnDraftUnsaved);
    }

    [Fact]
    public void ADraftOfAnotherTurnIsNotTheOneOnScreen()
    {
        var state = new MultiplayerUiState { PlanningTurn = 4, DelayedDraftTurn = 3 };

        Assert.False(state.OpenTurnDraftUnsaved);
    }
}

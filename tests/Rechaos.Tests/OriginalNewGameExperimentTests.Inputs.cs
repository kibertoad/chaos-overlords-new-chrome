using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // A fixture's inputs are what the original received. The probe lists a Done press, or a turn
    // left to the planning time limit, for each turn it played before the dump, and done_at_roll
    // has one entry for each, so the two agree even when the match ends or the human is eliminated
    // before --end-turns runs out (EXP-TURN-058).
    [Theory]
    [MemberData(nameof(Runs))]
    public void TheInputsListEveryTurnTheRunPlayedAndNoOther(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        Assert.Equal(recorded.DoneCount, recorded.TurnInputs);
        Assert.All(recorded.Orders, order => Assert.InRange(order.Turn, 1, recorded.DoneCount));
        Assert.All(recorded.Hires, hire => Assert.InRange(hire.Turn, 1, recorded.DoneCount));
        Assert.All(recorded.Search, write => Assert.InRange(write.Turn, 1, recorded.DoneCount));
        Assert.All(recorded.Planning, write => Assert.InRange(write.Turn, 1, recorded.DoneCount));
    }

    // The probe writes an order, a hire or a Search filter entry into the records of the human the
    // input names, and the replay acts for the same player, so each names a human of the run.
    [Theory]
    [MemberData(nameof(Runs))]
    public void EachOrderHireAndSearchWriteNamesAHumanOfTheRun(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var players = recorded.Orders.Select(order => order.Player)
            .Concat(recorded.Hires.Select(hire => hire.Player))
            .Concat(recorded.Search.Select(write => write.Player));
        Assert.All(players, player => Assert.Contains(new PlayerId(player), recorded.Humans));
    }
}

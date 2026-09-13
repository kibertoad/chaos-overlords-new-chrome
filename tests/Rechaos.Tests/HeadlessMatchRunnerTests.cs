using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class HeadlessMatchRunnerTests
{
    [Theory]
    [InlineData(AiPolicyMode.Original)]
    [InlineData(AiPolicyMode.Advanced)]
    public void SameSeedAndPolicyAreDeterministicAndReplayVerified(AiPolicyMode policy)
    {
        var definitions = BundledOriginalData.Load();
        var options = new HeadlessMatchOptions(
            ScenarioId.Power, GameDuration.FourYears, 4093, policy,
            ThroughTurn: 4, VerifyReplay: true);

        var first = HeadlessMatchRunner.Run(
            definitions, options, cancellationToken: TestContext.Current.CancellationToken);
        var second = HeadlessMatchRunner.Run(
            definitions, options, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first.ReplayVerified);
        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(MatchLimits.PlayerCount, first.State.Players.Count);
        Assert.All(first.State.Players,
            player => Assert.Equal(PlayerController.Computer, player.Setup.Controller));
    }

    [Fact]
    public void TurnWindowEmitsBoundedProgressWithoutPresentationRuntime()
    {
        var progress = new List<HeadlessMatchProgress>();
        var result = HeadlessMatchRunner.Run(
            BundledOriginalData.Load(),
            new HeadlessMatchOptions(
                ScenarioId.Siege, GameDuration.FourYears, 12289,
                ThroughTurn: 3, ProgressEveryTurns: 1),
            progress.Add,
            TestContext.Current.CancellationToken);

        Assert.True(result.State.Outcome is not null || result.State.Coordinator.Turn > 3);
        Assert.NotEmpty(progress);
        Assert.Equal(progress.OrderBy(item => item.Turn), progress);
        Assert.All(progress, item => Assert.True(item.PhaseBoundaries <= result.PhaseBoundaries));
    }

    [Fact]
    public void InvalidBoundsAreRejectedBeforeSimulation()
    {
        var definitions = BundledOriginalData.Load();

        Assert.Throws<ArgumentOutOfRangeException>(() => HeadlessMatchRunner.Run(
            definitions,
            new HeadlessMatchOptions(
                ScenarioId.Power, GameDuration.SixMonths, 1, ThroughTurn: 0),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeadlessMatchRunner.Run(
            definitions,
            new HeadlessMatchOptions(
                ScenarioId.Power, GameDuration.SixMonths, 1, ProgressEveryTurns: 0),
            cancellationToken: TestContext.Current.CancellationToken));
    }
}

using System.Text.Json;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class NativeSaveSerializerTests
{
    [Fact]
    public void CurrentSaveDoesNotPersistSyntheticSectorChaos()
    {
        using var document = JsonDocument.Parse(SaveBytes(CreateMatch()));

        Assert.All(
            document.RootElement.GetProperty("sectors").EnumerateArray(),
            sector => Assert.False(sector.TryGetProperty("chaos", out _)));
    }

    [Fact]
    public void RoundTripPreservesPendingSiteCompletionUntilNextPlanningBoundary()
    {
        var original = CreateMatch();
        var site = original.FindSite(1)!;
        site.Resistance = 1;
        original.FinishUpkeep();
        Assert.True(original.Submit(new GameCommand(
            new PlayerId(0), new GangId(0), GangAction.Influence,
            CommandTarget.Site(1))).Accepted);
        original.FinishCommand(new PlayerId(0));
        original.FinishCommand(new PlayerId(1));

        original.FinishExecutionPhase();

        Assert.Equal(0, site.Resistance);
        Assert.Null(site.InfluencedBy);
        var restored = RoundTrip(original);
        Assert.Null(restored.FindSite(1)!.InfluencedBy);
        Assert.Equal(JsonSerializer.Serialize(original.Events),
            JsonSerializer.Serialize(restored.Events));

        AdvanceToNextPlanning(original);
        AdvanceToNextPlanning(restored);

        Assert.Equal(new PlayerId(0), original.FindSite(1)!.InfluencedBy);
        Assert.Equal(original.FindSite(1)!.InfluencedBy, restored.FindSite(1)!.InfluencedBy);
        Assert.Equal(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(SaveBytes(original), SaveBytes(restored));
    }

    private static void AdvanceToNextPlanning(MatchState match)
    {
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        match.FinishUpkeep();
    }
}

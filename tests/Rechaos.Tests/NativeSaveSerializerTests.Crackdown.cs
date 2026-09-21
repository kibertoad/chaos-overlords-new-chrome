using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class NativeSaveSerializerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTripPreservesDecayedActiveCrackdownDuration(int remaining)
    {
        var match = CreateMatch();
        match.Sectors[12].CrackdownTurnsRemaining = remaining;

        var restored = RoundTrip(match);

        Assert.Equal(remaining, restored.Sectors[12].CrackdownTurnsRemaining);
        Assert.True(restored.Sectors[12].CrackdownActive);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
    }
}

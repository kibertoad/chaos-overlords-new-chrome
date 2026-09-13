using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangStatusMarkerPresentationTests
{
    [Fact]
    public void AnyIdleGangSelectsQuestionMarkStatus()
    {
        Assert.Equal(OriginalSpriteLayout.IdleGangStatus,
            GangStatusMarkerPresentation.Source(hasIdleGang: true));
        Assert.Equal(OriginalSpriteLayout.AssignedGangStatus,
            GangStatusMarkerPresentation.Source(hasIdleGang: false));
    }
}

using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireDockPolicyTests
{
    [Fact]
    public void AnOpenTurnCanHireAndSnub() =>
        Assert.Equal(
            HireDockAccess.Open,
            HireDockPolicy.Access(hasMatch: true, holdsTurn: true, turnIsSubmitted: false));

    [Fact]
    public void ASubmittedTurnCanStillBeBrowsed() =>
        Assert.Equal(
            HireDockAccess.ReadOnly,
            HireDockPolicy.Access(hasMatch: true, holdsTurn: false, turnIsSubmitted: true));

    [Fact]
    public void AMatchWithNeitherAHandleNorASubmittedTurnIsClosed() =>
        Assert.Equal(
            HireDockAccess.Closed,
            HireDockPolicy.Access(hasMatch: true, holdsTurn: false, turnIsSubmitted: false));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void WithoutAMatchNothingIsOnOffer(bool holdsTurn, bool turnIsSubmitted) =>
        Assert.Equal(
            HireDockAccess.Closed,
            HireDockPolicy.Access(hasMatch: false, holdsTurn, turnIsSubmitted));
}

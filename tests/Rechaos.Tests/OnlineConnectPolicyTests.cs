using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineConnectPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void PasswordAppliesToEveryJoinerAndOnlyToAListedHost(
        bool hosting, bool listedPublicly, bool applies) =>
        Assert.Equal(applies, OnlineConnectPolicy.PasswordApplies(hosting, listedPublicly));
}

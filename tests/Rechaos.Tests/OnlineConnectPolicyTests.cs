using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
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

    [Theory]
    [InlineData(true, MatchStatus.Lobby, false, true)]
    [InlineData(false, MatchStatus.Lobby, false, false)]
    [InlineData(true, MatchStatus.Running, false, false)]
    [InlineData(true, MatchStatus.Desynced, true, false)]
    [InlineData(true, MatchStatus.Lobby, true, false)]
    public void OnlyAFreshWaitingHostCanConfigureOrStart(
        bool isHost, MatchStatus status, bool joinedInProgress, bool allowed) =>
        Assert.Equal(
            allowed,
            OnlineConnectPolicy.CanConfigureLobby(isHost, status, joinedInProgress));
}

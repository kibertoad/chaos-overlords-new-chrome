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

    /// <summary>
    /// A call that would be dropped is never offered.
    /// </summary>
    /// <remarks>
    /// The lobby runs one call at a time and drops the second, so a button live over an in-flight
    /// one is a button that does nothing. The JOIN that announced JOINING LOBBY and then stranded
    /// the player there was this case: pressed over a REFRESH, never sent, and answered instead by
    /// the browse reply that put them back in the browser.
    /// </remarks>
    [Theory]
    [InlineData(true, false, 1, true)]
    [InlineData(true, true, 1, false)]
    [InlineData(false, false, 1, false)]
    [InlineData(true, false, 0, false)]
    [InlineData(true, true, 0, false)]
    public void NoLobbyCallIsOfferedOverOneAlreadyInFlight(
        bool hasLobby, bool lobbyIsBusy, int subjects, bool allowed) =>
        Assert.Equal(allowed, OnlineConnectPolicy.CanCallLobby(hasLobby, lobbyIsBusy, subjects));

    /// <summary>A control with no selection asks only whether the lobby is free.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AControlWithNothingToSelectAsksOnlyWhetherTheLobbyIsFree(
        bool lobbyIsBusy, bool allowed) =>
        Assert.Equal(allowed, OnlineConnectPolicy.CanCallLobby(hasLobby: true, lobbyIsBusy));
}

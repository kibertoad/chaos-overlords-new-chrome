using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

/// <summary>
/// Which removal vote a player is asked about, and how the interface counts one, as the server does.
/// </summary>
public sealed class OnlineRemovalUiTests
{
    private static IReadOnlyDictionary<string, RemovalChoice> Votes(
        params (string Voter, RemovalChoice Choice)[] votes) =>
        votes.ToDictionary(vote => vote.Voter, vote => vote.Choice, StringComparer.Ordinal);

    [Fact]
    public void APlayerIsNeverAskedAboutRemovingThemselves()
    {
        var open = new[] { ("p1", Votes(("p2", RemovalChoice.Remove))) };

        Assert.Null(RemovalVotePolicy.SeatToVoteOn(open, "p1"));
        Assert.Equal("p1", RemovalVotePolicy.SeatToVoteOn(open, "p3"));
    }

    /// <summary>
    /// A player who has answered is not asked again, whichever way they answered: the vote stays
    /// open while anybody holds remove, and a player who chose keep would otherwise sit behind the
    /// modal for as long as somebody disagreed with them.
    /// </summary>
    [Fact]
    public void AnAnsweredVoteIsNotAskedAgain()
    {
        var open = new[]
        {
            ("p1", Votes(("p2", RemovalChoice.Remove), ("p3", RemovalChoice.Keep))),
            ("p4", Votes(("p2", RemovalChoice.Remove))),
        };

        Assert.Equal("p4", RemovalVotePolicy.SeatToVoteOn(open, "p3"));
        Assert.Null(RemovalVotePolicy.SeatToVoteOn(open, "p2"));
    }

    [Fact]
    public void TheTallyCountsEveryActivePlayerButTheSeat()
    {
        IReadOnlyList<PlayerView> roster =
        [
            new("p1", 0, "ADA", 0, WirePlayerStatus.Active, IsHost: true),
            new("p2", 1, "GRACE", 1, WirePlayerStatus.Active, IsHost: false),
            new("p3", 2, "LINUS", 2, WirePlayerStatus.Active, IsHost: false),
            new("p4", 3, "ALAN", 3, WirePlayerStatus.Left, IsHost: false),
        ];
        var votes = Votes(
            ("p2", RemovalChoice.Remove), ("p3", RemovalChoice.Keep), ("p4", RemovalChoice.Remove));

        Assert.Equal((1, 2), RemovalVotePolicy.Tally(roster, "p1", votes));
    }

    [Fact]
    public void ARemovedSeatCannotBeVotedOnAgain()
    {
        Assert.False(RemovalVotePolicy.IsRemovable(
            new PlayerView("p2", 1, "GRACE", 1, WirePlayerStatus.Kicked, IsHost: false)));
        Assert.True(RemovalVotePolicy.IsRemovable(
            new PlayerView("p2", 1, "GRACE", 1, WirePlayerStatus.Computer, IsHost: false)));
    }
}

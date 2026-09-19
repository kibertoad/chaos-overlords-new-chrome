using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What an online match shows a player about a missed deadline: theirs, and everybody else's.
/// </summary>
public sealed class OnlineAbsenceUiTests
{
    [Fact]
    public void TheVotePutToAPlayerIsNeverAboutTheirOwnSeat()
    {
        var open = new[] { ("p1", 4) };

        Assert.Null(TakeoverVotePolicy.SeatToVoteOn(open, "p1"));
        Assert.Equal("p1", TakeoverVotePolicy.SeatToVoteOn(open, "p2"));
    }

    /// <summary>
    /// A player waiting out a vote about themselves is still asked about everybody else.
    /// </summary>
    /// <remarks>
    /// Two seats can be absent at once — the second one is why the first is taking so long to come
    /// back — and the seat that is this client's own must not hide the question it can answer.
    /// </remarks>
    [Fact]
    public void AnotherAbsenceIsStillPutToAPlayerWhoIsThemselvesBeingVotedOn()
    {
        var open = new[] { ("p1", 4), ("p2", 4) };

        Assert.Equal("p2", TakeoverVotePolicy.SeatToVoteOn(open, "p1"));
    }

    /// <summary>The oldest absence first, and the player id to break a tie every client shares.</summary>
    [Fact]
    public void TheLongestOutstandingAbsenceIsAskedAboutFirst()
    {
        Assert.Equal(
            "p3", TakeoverVotePolicy.SeatToVoteOn([("p2", 9), ("p3", 4), ("p4", 9)], "p1"));
        Assert.Equal(
            "p2", TakeoverVotePolicy.SeatToVoteOn([("p4", 9), ("p2", 9)], "p1"));
    }

    [Fact]
    public void NoOpenVoteIsNoQuestion() =>
        Assert.Null(TakeoverVotePolicy.SeatToVoteOn([], "p1"));
}

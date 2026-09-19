using Rechaos.Game;
using Rechaos.Multiplayer.Session;
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

    /// <summary>
    /// A match that has ended is asking nobody about anything.
    /// </summary>
    /// <remarks>
    /// The modal owns the keyboard and the mouse on whatever screen the player is on, and the
    /// endgame is a screen they get past with the keyboard. Left standing over it, the prompt
    /// swallowed those keys while the server had already begun refusing its two buttons — a match
    /// that is not running takes no votes — so the only way out of a finished match was the escape
    /// menu. The player's own absence is dropped with it: the turn status line would otherwise go
    /// on asking the winner to wait for a vote on their seat.
    /// </remarks>
    [Fact]
    public void AMatchThatHasEndedLeavesNoAbsenceVoteStandingOverTheEndgame()
    {
        var online = OnlineWith(Absence("p1", 4), Absence("p2", 4));
        online.DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(30);

        online.ConcludeMatch();

        Assert.Equal(MultiplayerStage.Finished, online.Stage);
        Assert.Null(online.CurrentTakeoverVote);
        Assert.Null(online.OwnTakeoverVote);
        Assert.Null(online.DeadlineAt);
    }

    /// <summary>
    /// The announcement of a vote can outlive the match it was about.
    /// </summary>
    /// <remarks>
    /// Clearing the open votes as the match ends is not enough on its own. Event delivery is at
    /// least once and is not ordered against the outcome, and the absence that reaches this client
    /// last is the likeliest one to arrive late: a player who runs out of time on the turn that
    /// decides the match is marked absent by that very seal.
    /// </remarks>
    [Fact]
    public void AnAbsenceAnnouncedAfterTheMatchEndedIsNotPutToAnybody()
    {
        var online = OnlineWith();
        online.ConcludeMatch();

        online.RecordTakeoverVote(Absence("p2", 9));
        online.RecordTakeoverVote(Absence("p1", 9));

        Assert.Null(online.CurrentTakeoverVote);
        Assert.Null(online.OwnTakeoverVote);
    }

    /// <summary>A vote the server settles stops being asked about, while the match is still on.</summary>
    [Fact]
    public void ASettledVoteIsNoLongerAskedAbout()
    {
        var online = OnlineWith(Absence("p2", 4));
        Assert.NotNull(online.CurrentTakeoverVote);

        online.CloseTakeoverVote("p2");

        Assert.Null(online.CurrentTakeoverVote);
    }

    /// <summary>A running match seated as "p1", carrying the absences given.</summary>
    private static MultiplayerUiState OnlineWith(params TakeoverVotePrompt[] absences)
    {
        var online = new MultiplayerUiState
        {
            SelfPlayerId = "p1",
            Stage = MultiplayerStage.Playing,
        };
        foreach (var absence in absences) online.RecordTakeoverVote(absence);
        return online;
    }

    private static TakeoverVotePrompt Absence(string playerId, int turn) =>
        new(playerId, playerId.ToUpperInvariant(), turn,
            new Dictionary<string, TakeoverChoice>(StringComparer.Ordinal));
}

using Microsoft.Xna.Framework;
using Rechaos.Game;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed class ReconnectPopupTests
{
    /// <summary>Every attempt's copy button sits inside the modal, beside its line and above STOP RETRYING.</summary>
    [Fact]
    public void EachAttemptsCopyButtonStandsBesideItsLine()
    {
        var panel = ReconnectPopupLayout.Panel;
        for (var index = 0; index < ReconnectPopupLayout.MaxRows; index++)
        {
            var copy = ReconnectPopupLayout.CopyError(index);
            Assert.True(panel.Contains(copy));
            Assert.False(copy.Intersects(ReconnectPopupLayout.StopRetrying));
            Assert.True(copy.Bottom < ReconnectPopupLayout.CopyStatusTop);
            var textEnd = ReconnectPopupLayout.TextLeft
                + ReconnectPopupLayout.SummaryColumns * OriginalFontLayout.CellWidth;
            Assert.True(textEnd < copy.Left);
            if (index > 0) Assert.False(copy.Intersects(ReconnectPopupLayout.CopyError(index - 1)));
            Assert.Equal(index, ReconnectPopupLayout.CopyErrorAt(copy.Center, ReconnectPopupLayout.MaxRows));
        }
    }

    [Fact]
    public void OnlyTheButtonsOfDrawnAttemptsAnswerAClick()
    {
        var third = ReconnectPopupLayout.CopyError(2).Center;
        Assert.Null(ReconnectPopupLayout.CopyErrorAt(third, rows: 2));
        Assert.Null(ReconnectPopupLayout.CopyErrorAt(ReconnectPopupLayout.StopRetrying.Center, rows: 6));
    }

    /// <summary>The copied text keeps what the modal line cannot: the lane, the time and the whole exception.</summary>
    [Fact]
    public void TheCopiedDetailsCarryTheWholeError()
    {
        var error = new IOException("The server closed the event stream.", new TimeoutException("idle"));
        var entry = ReconnectAttemptEntry.From(
            new MultiplayerNotice.ConnectionChanged(
                false, "Connection stream failed: The server closed the event stream.", 3, "stream", error),
            new DateTimeOffset(2026, 9, 23, 14, 5, 6, 789, TimeSpan.FromHours(2)));

        Assert.Equal(3, entry.Attempt);
        Assert.Equal("ATTEMPT 3  CONNECTION STREAM FAILED: THE SERVER CLOSED THE EVENT STREAM.", entry.Summary);
        Assert.Contains("Reconnect attempt 3", entry.Details);
        Assert.Contains("Time: 2026-09-23T12:05:06.789Z", entry.Details);
        Assert.Contains("Connection: stream", entry.Details);
        Assert.Contains("Summary: Connection stream failed: The server closed the event stream.", entry.Details);
        Assert.Contains("System.IO.IOException: The server closed the event stream.", entry.Details);
        Assert.Contains("System.TimeoutException: idle", entry.Details);
    }

    [Fact]
    public void AnAttemptWithoutAnExceptionStillCopiesItsDetail()
    {
        var entry = ReconnectAttemptEntry.From(
            new MultiplayerNotice.ConnectionChanged(false, "Still trying.", 0),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(1, entry.Attempt);
        Assert.DoesNotContain("Error:", entry.Details);
        Assert.EndsWith("Summary: Still trying.", entry.Details);
    }

    /// <summary>A lane's failure reaches the callback with its name and what was thrown.</summary>
    [Fact]
    public void AFailedLaneReportsItsNameAndError()
    {
        ConnectionFailure? reported = null;
        var health = new ConnectionHealth(failure => reported = failure);
        var error = new InvalidOperationException("boom");

        health.Open("outbox").Failed("outbox down", 2, error);

        Assert.Equal(new ConnectionFailure("outbox", "outbox down", 2, error), reported);
    }
}

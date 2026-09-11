using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ComlinkTests
{
    [Fact]
    public void InboxRetainsNewestSixteenAndTracksUnreadMessages()
    {
        var inbox = new ComlinkInbox();
        for (var index = 0; index < MatchLimits.ComlinkMessagesPerPlayer; index++)
            inbox.Receive(index + 1, new PlayerId(1), $"MESSAGE {index}");

        inbox.MarkAllRead();
        Assert.False(inbox.HasUnread);

        var newest = inbox.Receive(17, new PlayerId(2), "NEWEST");

        Assert.Equal(MatchLimits.ComlinkMessagesPerPlayer, inbox.Count);
        Assert.Equal(1, inbox.Messages[0].Sequence);
        Assert.Equal(newest, inbox.Messages[^1]);
        Assert.True(inbox.HasUnread);
    }

    [Fact]
    public void InboxRejectsMessagesOutsideRecoveredRecordCapacity()
    {
        var inbox = new ComlinkInbox();

        Assert.Throws<ArgumentException>(() => inbox.Receive(1, new PlayerId(0), ""));
        Assert.Throws<ArgumentException>(() => inbox.Receive(
            1, new PlayerId(0), new string('X', MatchLimits.ComlinkMessageCharacters + 1)));
    }
}

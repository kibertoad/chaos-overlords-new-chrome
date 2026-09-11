using Rechaos.Core.Assets;
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

    [Fact]
    public void MatchDeliveryIsHumanOnlyAndChangesCanonicalState()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human),
            new(new PlayerId(2), "CPU", PlayerController.Computer)
        ];
        var match = OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        match.FinishUpkeep();
        var before = MatchStateHasher.ComputeSha256(match);

        var accepted = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "WATCH YOUR BACK");
        var rejected = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(2)], "COMPUTERS CANNOT READ THIS");

        Assert.True(accepted.Accepted);
        Assert.Equal(ComlinkValidationCode.RecipientNotHuman, rejected.Code);
        Assert.Equal("WATCH YOUR BACK", match.ComlinkFor(new PlayerId(1)).Messages[0].Text);
        Assert.True(match.ComlinkFor(new PlayerId(1)).HasUnread);
        Assert.NotEqual(before, MatchStateHasher.ComputeSha256(match));
        Assert.True(match.MarkComlinkRead(new PlayerId(1)));
        Assert.False(match.ComlinkFor(new PlayerId(1)).HasUnread);
    }
}

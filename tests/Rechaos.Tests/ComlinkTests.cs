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

    // RULE-COMLINK-007: ending planning drops the read messages at the front, up to the first
    // unread one; a read message after it stays.
    [Fact]
    public void EndingPlanningDropsTheLeadingReadMessages()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human)
        ];
        var match = OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        match.FinishUpkeep();
        foreach (var text in new[] { "FIRST", "SECOND", "THIRD", "FOURTH" })
            Assert.True(match.SendComlinkMessage(new PlayerId(0), [new PlayerId(1)], text).Accepted);
        var inbox = match.ComlinkFor(new PlayerId(1));
        match.MarkComlinkRead(new PlayerId(1), inbox.Messages[0].Sequence);
        match.MarkComlinkRead(new PlayerId(1), inbox.Messages[1].Sequence);
        match.MarkComlinkRead(new PlayerId(1), inbox.Messages[3].Sequence);

        match.FinishCommand(new PlayerId(0));
        Assert.Equal(4, inbox.Count);
        match.FinishCommand(new PlayerId(1));

        Assert.Equal(["THIRD", "FOURTH"], inbox.Messages.Select(message => message.Text));
        Assert.True(inbox.IsRead(inbox.Messages[1].Sequence));
        using var save = new MemoryStream();
        Rechaos.Core.Persistence.NativeSaveSerializer.Save(save, match);
        save.Position = 0;
        var restored = Rechaos.Core.Persistence.NativeSaveSerializer.Load(save, data);
        Assert.Equal(2, restored.ComlinkFor(new PlayerId(1)).Count);
    }

    // RULE-COMLINK-004, FMT-STATE-005: the original's save holds no messages and entering a match
    // empties every record, so after a load View finds no message and does not open.
    [Fact]
    public void EnteringALoadedMatchEmptiesEveryInbox()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human),
            new(new PlayerId(2), "THREE", PlayerController.Human)
        ];
        var match = OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        match.FinishUpkeep();
        Assert.True(match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1), new PlayerId(2)], "TRUCE?").Accepted);
        Assert.True(match.SendComlinkMessage(new PlayerId(0), [new PlayerId(1)], "ANSWER").Accepted);
        match.MarkComlinkRead(new PlayerId(1), match.ComlinkFor(new PlayerId(1)).Messages[0].Sequence);
        using var save = new MemoryStream();
        Rechaos.Core.Persistence.NativeSaveSerializer.Save(save, match);
        save.Position = 0;
        var loaded = Rechaos.Core.Persistence.NativeSaveSerializer.Load(save, data);
        var next = loaded.ComlinkFor(new PlayerId(1)).NextSequence;

        Assert.True(loaded.EmptyComlinkInboxes());

        foreach (var player in loaded.Players)
        {
            var inbox = loaded.ComlinkFor(player.Id);
            Assert.Equal(0, inbox.Count);
            Assert.Empty(inbox.ReadSequences);
            Assert.False(inbox.HasUnread);
        }
        Assert.False(loaded.EmptyComlinkInboxes());
        // A message after the load takes a new number, so no stale read mark can reach it.
        Assert.True(loaded.SendComlinkMessage(new PlayerId(0), [new PlayerId(1)], "AGAIN").Accepted);
        var again = Assert.Single(loaded.ComlinkFor(new PlayerId(1)).Messages);
        Assert.Equal(next, again.Sequence);
        Assert.False(loaded.ComlinkFor(new PlayerId(1)).IsRead(again.Sequence));
        using var resaved = new MemoryStream();
        Rechaos.Core.Persistence.NativeSaveSerializer.Save(resaved, loaded);
        resaved.Position = 0;
        Assert.Single(Rechaos.Core.Persistence.NativeSaveSerializer.Load(resaved, data)
            .ComlinkFor(new PlayerId(1)).Messages);
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
        var before = MatchStateHasher.ComputeFingerprint(match);

        var accepted = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "WATCH YOUR BACK");
        var second = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "THE LATEST WORD");
        var rejected = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(2)], "COMPUTERS CANNOT READ THIS");

        Assert.True(accepted.Accepted);
        Assert.True(second.Accepted);
        Assert.Equal(ComlinkValidationCode.RecipientNotHuman, rejected.Code);
        Assert.Equal("WATCH YOUR BACK", match.ComlinkFor(new PlayerId(1)).Messages[0].Text);
        Assert.True(match.ComlinkFor(new PlayerId(1)).HasUnread);
        Assert.NotEqual(before, MatchStateHasher.ComputeFingerprint(match));
        var inbox = match.ComlinkFor(new PlayerId(1));
        Assert.True(match.MarkComlinkRead(new PlayerId(1), inbox.Messages[1].Sequence));
        Assert.True(inbox.HasUnread);
        Assert.True(inbox.IsRead(inbox.Messages[1].Sequence));
        Assert.False(inbox.IsRead(inbox.Messages[0].Sequence));
        Assert.False(match.MarkComlinkRead(new PlayerId(1), inbox.Messages[1].Sequence));
        Assert.True(match.MarkComlinkRead(new PlayerId(1), inbox.Messages[0].Sequence));
        Assert.False(match.ComlinkFor(new PlayerId(1)).HasUnread);
    }
}

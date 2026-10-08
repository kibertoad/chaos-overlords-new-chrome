using Microsoft.Xna.Framework;
using Rechaos.Game;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed class LobbyChatPresentationTests
{
    [Fact]
    public void LabelsEachMessageWithItsAuthorAndWrapsAtSpaces()
    {
        var rows = LobbyChatPresentation.Rows(
            [new LobbyChatLine(1, "p1", "TAKE THE NORTH AND I TAKE THE SOUTH")],
            _ => "ANNA",
            columns: 20,
            rows: 10);

        Assert.Equal(["ANNA: TAKE THE NORTH", "AND I TAKE THE SOUTH"], rows);
    }

    [Fact]
    public void ANoticeIsDrawnWithoutAnAuthor()
    {
        var rows = LobbyChatPresentation.Rows(
            [
                new LobbyChatLine(1, "p1", "HI"),
                new LobbyChatLine(2, string.Empty, SpectatorAnnouncement.Joined("EVE"), IsNotice: true),
            ],
            _ => "ANNA",
            columns: 40,
            rows: 10);

        Assert.Equal(["ANNA: HI", $"* {SpectatorAnnouncement.Joined("EVE")}"], rows);
    }

    [Fact]
    public void CutsAWordLongerThanARow()
    {
        var rows = LobbyChatPresentation.Rows(
            [new LobbyChatLine(1, "p1", "ABCDEFGHIJKLMNOP")], _ => "B", columns: 8, rows: 10);

        Assert.Equal(["B:", "ABCDEFGH", "IJKLMNOP"], rows);
    }

    [Fact]
    public void ShortensLongNamesSoTheTextKeepsItsRoom()
    {
        var rows = LobbyChatPresentation.Rows(
            [new LobbyChatLine(1, "p1", "HI")], _ => "AVERYLONGOVERLORDNAME", columns: 40, rows: 1);

        Assert.Equal(["AVERYLONGO: HI"], rows);
    }

    [Fact]
    public void ShowsTheNewestRowsWhenTheConversationOverflows()
    {
        var messages = Enumerable.Range(1, 6)
            .Select(seq => new LobbyChatLine(seq, "p1", $"MESSAGE {seq}"))
            .ToArray();

        var rows = LobbyChatPresentation.Rows(messages, _ => "C", columns: 30, rows: 2);

        Assert.Equal(["C: MESSAGE 5", "C: MESSAGE 6"], rows);
    }

    [Fact]
    public void ARereadMessageIsKeptOnceAndOnlyTheNewestAreKept()
    {
        var state = new MultiplayerUiState();
        state.RecordChat([new LobbyChatLine(1, "p1", "A"), new LobbyChatLine(2, "p2", "B")]);
        state.RecordChat([new LobbyChatLine(1, "p1", "A"), new LobbyChatLine(2, "p2", "B"),
            new LobbyChatLine(4, "p1", "C")]);

        Assert.Equal([1, 2, 4], state.ChatLines.Select(line => line.Seq));

        state.RecordChat(Enumerable.Range(10, LobbyChatPresentation.KeptMessages)
            .Select(seq => new LobbyChatLine(seq, "p1", "X")));
        Assert.Equal(LobbyChatPresentation.KeptMessages, state.ChatLines.Count);
        Assert.Equal(10, state.ChatLines[0].Seq);

        state.Reset();
        Assert.Empty(state.ChatLines);
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('a', true)]
    [InlineData('7', true)]
    [InlineData('?', true)]
    [InlineData('\b', true)]
    [InlineData('\t', false)]
    [InlineData('é', false)]
    [InlineData('‮', false)]
    public void TheInputTakesOnlyCharactersTheOriginalFontDraws(char character, bool accepted) =>
        Assert.Equal(accepted, LobbyChatPresentation.Accepts(character));

    [Fact]
    public void TheModernLobbyChatSitsUnderTheRosterAboveTheWaitingLine()
    {
        var log = OnlineLobbyLayout.ChatLog;
        var input = OnlineLobbyLayout.ChatInput;
        var lastSeat = OnlineLobbyLayout.RosterPortrait(5);

        Assert.True(OnlineLobbyLayout.ProfileHintY + OriginalFontLayout.GlyphHeight
            <= OnlineLobbyLayout.ChatCaptionY);
        Assert.True(lastSeat.Bottom <= OnlineLobbyLayout.ChatCaptionY);
        Assert.True(OnlineLobbyLayout.ChatCaptionY + OriginalFontLayout.GlyphHeight <= log.Y);
        Assert.True(log.Bottom <= input.Y);
        Assert.True(input.Bottom <= OnlineLobbyLayout.WaitingHintY);
        Assert.True(log.Right <= OnlineLobbyLayout.SettingsLeft);
        Assert.All(OnlineLobbyLayout.HostSettings.Append(OnlineLobbyLayout.Setup),
            control => Assert.False(control.Intersects(log) || control.Intersects(input)));
    }

    [Fact]
    public void TheClassicLobbyChatStaysClearOfEveryMappedControl()
    {
        Rectangle[] controls =
        [
            ClassicOnlineLobbyLayout.SessionName,
            ClassicOnlineLobbyLayout.CopyCode,
            ClassicOnlineLobbyLayout.PublicChoice,
            ClassicOnlineLobbyLayout.PrivateChoice,
            ClassicOnlineLobbyLayout.LateJoinAllowed,
            ClassicOnlineLobbyLayout.LateJoinRefused,
            ClassicOnlineLobbyLayout.WatchRefused,
            ClassicOnlineLobbyLayout.WatchAllowed,
            ClassicOnlineLobbyLayout.WatchSooner,
            ClassicOnlineLobbyLayout.WatchLater,
            ClassicOnlineLobbyLayout.Setup,
            ClassicOnlineLobbyLayout.Spectators,
            ClassicOnlineLobbyLayout.Start,
            ClassicOnlineLobbyLayout.Leave,
            ClassicOnlineLobbyLayout.Roster,
        ];
        var log = ClassicOnlineLobbyLayout.ChatLog;
        var input = ClassicOnlineLobbyLayout.ChatInput;

        Assert.False(log.Intersects(input));
        Assert.True(input.Bottom <= 460);
        Assert.All(controls,
            control => Assert.False(control.Intersects(log) || control.Intersects(input)));
    }
}

using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ComlinkUiTests
{
    [Fact]
    public void PanelsUseRecoveredOriginalArtworkGeometry()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), ComlinkViewLayout.Panel);
        Assert.Equal(ComlinkViewLayout.Panel, ComlinkSendLayout.Panel);
        Assert.Equal(new Rectangle(215, 170, 64, 64), ComlinkViewLayout.SenderPortrait);
        Assert.Equal(new Rectangle(198, 247, 238, 34), ComlinkViewLayout.Message);
        Assert.Equal(new Rectangle(135, 157, 26, 23), ComlinkViewLayout.Previous);
        Assert.Equal(new Rectangle(163, 157, 26, 23), ComlinkViewLayout.Next);
        Assert.Equal(new Rectangle(137, 293, 49, 22), ComlinkViewLayout.Ok);
        Assert.Equal(new Point(199, 256), ComlinkSendLayout.TextOrigin);
        Assert.Equal(new Rectangle(199, 256, 240, 31), ComlinkSendLayout.Message);
        Assert.Equal(8, ComlinkSendLayout.TextRowStride);
        Assert.Equal(new Rectangle(199, 256, 6, 7), ComlinkSendLayout.CaretDestination(0, 0));
        Assert.Equal(new Rectangle(433, 280, 6, 7), ComlinkSendLayout.CaretDestination(39, 3));
        Assert.Equal(new Rectangle(198, 0, 6, 7), ComlinkSendLayout.CaretSource('a', inverse: false));
        Assert.Equal(new Rectangle(198, 441, 6, 7), ComlinkSendLayout.CaretSource('a', inverse: true));
        Assert.Equal(new Rectangle(137, 261, 49, 22), ComlinkSendLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), ComlinkSendLayout.Ok);
        Assert.Equal(new Rectangle(137, 261, 50, 23), ComlinkSendLayout.CancelPressed);
        Assert.Equal(new Rectangle(137, 293, 50, 23), ComlinkSendLayout.OkPressed);
        Assert.Equal(new Rectangle(50, 409, 50, 23), ComlinkSendLayout.CancelPressedSource);
        Assert.Equal(new Rectangle(50, 386, 50, 23), ComlinkSendLayout.OkPressedSource);
        Assert.Equal(new Rectangle(201, 143, 105, 34), ComlinkSendLayout.Recipient(0));
        Assert.Equal(new Rectangle(322, 211, 105, 34), ComlinkSendLayout.Recipient(5));
        Assert.Equal(new Rectangle(202, 144, 100, 32), ComlinkSendLayout.RecipientHit(0));
        Assert.Equal(new Rectangle(323, 212, 100, 32), ComlinkSendLayout.RecipientHit(5));
        Assert.Equal(new Rectangle(203, 145, 7, 30), ComlinkSendLayout.RecipientAccent(0));
        Assert.Equal(new Rectangle(210, 144, 32, 32), ComlinkSendLayout.RecipientPortrait(0));
        Assert.Equal(new Point(243, 145), ComlinkSendLayout.RecipientNameOrigin(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComlinkSendLayout.Recipient(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComlinkSendLayout.RecipientHit(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComlinkSendLayout.CaretDestination(40, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComlinkSendLayout.CaretSource('[', inverse: false));
    }

    [Fact]
    public void EditorUsesRecoveredFourByFortyOverwriteCursor()
    {
        var editor = new ComlinkTextEditor();
        Assert.True(editor.TryAppend('a'));
        Assert.True(editor.TryAppend('b'));
        editor.MoveLeft();
        Assert.True(editor.TryAppend('z'));
        Assert.Equal("AZ", editor.Text);
        Assert.Equal(2, editor.Column);
        Assert.Equal(0, editor.Row);

        editor.MoveNextRow();
        Assert.Equal(0, editor.Column);
        Assert.Equal(1, editor.Row);
        Assert.True(editor.TryAppend('q'));
        Assert.Equal('Q', editor.DisplayLines()[1][0]);
        Assert.Equal(' ', editor.CharacterAtCursor);

        editor.MoveUp();
        editor.MoveLeft();
        Assert.True(editor.Backspace());
        Assert.Equal(' ', editor.DisplayLines()[0][39]);
        Assert.Equal("AZ" + new string(' ', 38) + "Q", editor.Text);
        Assert.False(editor.TryAppend('~'));
    }

    // RULE-COMLINK-003, RULE-COMLINK-006, FMT-STATE-005: a draft of spaces only is dropped once
    // a recipient is chosen, and the panel closes as if it had been sent. The stored text drops
    // trailing spaces and keeps the others, so padding it back to 160 gives the original's record.
    [Fact]
    public void BlankDraftIsDroppedAndTextPadsBackToTheOriginalRecord()
    {
        var editor = new ComlinkTextEditor();
        Assert.True(editor.IsBlank);
        Assert.False(ChaosGame.DropsBlankComlinkMessage(anyRecipient: false, editor));
        Assert.True(ChaosGame.DropsBlankComlinkMessage(anyRecipient: true, editor));
        Assert.True(editor.TryAppend(' '));
        Assert.True(editor.IsBlank);
        Assert.True(ChaosGame.DropsBlankComlinkMessage(anyRecipient: true, editor));

        editor.MoveNextRow();
        Assert.True(editor.TryAppend(' '));
        Assert.True(editor.TryAppend('h'));
        Assert.True(editor.TryAppend(' '));
        Assert.False(editor.IsBlank);
        Assert.False(ChaosGame.DropsBlankComlinkMessage(anyRecipient: true, editor));
        var expected = new string(' ', 40) + " H" + new string(' ', 118);
        Assert.Equal(expected.TrimEnd(), editor.Text);
        Assert.Equal(expected, editor.Text.PadRight(MatchLimits.ComlinkMessageCharacters));
    }

    [Fact]
    public void EditorWrapsColumnsAndClampsRowsLikeOriginalHandler()
    {
        var editor = new ComlinkTextEditor();
        for (var index = 0; index < ComlinkSendLayout.MessageColumns; index++)
            Assert.True(editor.TryAppend('X'));
        Assert.Equal(0, editor.Column);
        Assert.Equal(1, editor.Row);

        editor.MoveUp();
        editor.MoveLeft();
        Assert.Equal(39, editor.Column);
        Assert.Equal(0, editor.Row);

        editor.MoveNextRow();
        editor.MoveNextRow();
        editor.MoveNextRow();
        editor.MoveNextRow();
        Assert.Equal(0, editor.Column);
        Assert.Equal(3, editor.Row);
    }

    [Fact]
    public void ViewStartsAtFirstUnreadMessageAndOtherwiseKeepsBoundedPage()
    {
        var inbox = new ComlinkInbox();
        inbox.Receive(1, new PlayerId(0), "FIRST");
        inbox.Receive(1, new PlayerId(1), "SECOND");
        inbox.Receive(1, new PlayerId(2), "THIRD");
        inbox.MarkRead(0);

        Assert.Equal(1, ChaosGame.InitialComlinkViewCursor(inbox, 2));

        inbox.MarkRead(1);
        inbox.MarkRead(2);
        Assert.Equal(2, ChaosGame.InitialComlinkViewCursor(inbox, 2));
        Assert.Equal(2, ChaosGame.InitialComlinkViewCursor(inbox, 99));
        Assert.Equal(0, ChaosGame.InitialComlinkViewCursor(new ComlinkInbox(), 99));
    }

    [Fact]
    public void ComlinkScreensParticipateInPanelNavigation()
    {
        Assert.True(PanelSlideTransition.IsPanel(ClientScreen.ComlinkView));
        Assert.True(PanelSlideTransition.IsPanel(ClientScreen.ComlinkSend));
        var router = new ScreenRouter();
        router.Show(ClientScreen.ComlinkSend);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
    }

    [Fact]
    public void SendCaretStartsNormalAndTogglesEveryThreeSixHertzTimerEvents()
    {
        var cadence = new ComlinkCaretCadence();
        cadence.Reset(TimeSpan.Zero);

        Assert.False(cadence.UsesInverseGlyph);
        cadence.Advance(ComlinkCaretCadence.TimerEventInterval * 2);
        Assert.False(cadence.UsesInverseGlyph);
        cadence.Advance(ComlinkCaretCadence.TimerEventInterval * 3);
        Assert.True(cadence.UsesInverseGlyph);
        cadence.Advance(ComlinkCaretCadence.TimerEventInterval * 6);
        Assert.False(cadence.UsesInverseGlyph);
    }
}

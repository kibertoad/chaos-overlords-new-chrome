using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// SCR-SETUP-003: the setup name editor keeps the text, insertion point and selection of the
/// name dialog's edit control (FND-UI-068, SRC-WIN32-EDIT), and OK keeps what the copy helper
/// keeps (FND-UI-022, FND-UI-068). The card shows ten characters of the text and a caret drawn
/// by the rebuild (DEV-SETUP-003).
/// </summary>
public sealed class SetupNameEditControlTests
{
    private static SetupPlayerNameEditor Typed(string text)
    {
        var editor = new SetupPlayerNameEditor();
        editor.Begin(string.Empty);
        foreach (var character in text) Assert.True(editor.Type(character));
        return editor;
    }

    [Fact]
    public void EditorStartsEmptyWithTheCaretAtTheStart()
    {
        var editor = new SetupPlayerNameEditor();
        editor.Begin(string.Empty);

        Assert.Equal(string.Empty, editor.Text);
        Assert.Equal(0, editor.Caret);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void FocusingTheControlSelectsItsText()
    {
        var editor = new SetupPlayerNameEditor();
        editor.Begin("ABC");

        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionEnd);
        Assert.Equal(3, editor.Caret);
        Assert.True(editor.Type('X'));
        Assert.Equal("X", editor.Text);
    }

    [Fact]
    public void UppercaseStyleCapitalisesLettersAndControlCharactersAreNotText()
    {
        var editor = Typed("ab_c^1");

        Assert.Equal("AB_C^1", editor.Text);
        Assert.False(editor.Type('\b'));
        Assert.False(editor.Type('\r'));
        Assert.False(editor.Type((char)27));
        Assert.False(editor.Type((char)127));
        Assert.Equal("AB_C^1", editor.Text);
    }

    [Fact]
    public void CaretKeysMoveTheInsertionPointAndTypingInsertsThere()
    {
        var editor = Typed("ACE");

        Assert.True(editor.Press(Keys.Left, shift: false));
        Assert.True(editor.Press(Keys.Left, shift: false));
        Assert.Equal(1, editor.Caret);
        editor.Type('b');
        Assert.Equal("ABCE", editor.Text);
        Assert.Equal(2, editor.Caret);
        editor.Press(Keys.Right, shift: false);
        editor.Type('d');
        Assert.Equal("ABCDE", editor.Text);
        editor.Press(Keys.Home, shift: false);
        Assert.Equal(0, editor.Caret);
        editor.Press(Keys.Left, shift: false);
        Assert.Equal(0, editor.Caret);
        editor.Type('>');
        Assert.Equal(">ABCDE", editor.Text);
        editor.Press(Keys.End, shift: false);
        Assert.Equal(6, editor.Caret);
        editor.Press(Keys.Right, shift: false);
        Assert.Equal(6, editor.Caret);
        Assert.False(editor.Press(Keys.A, shift: false));
    }

    [Fact]
    public void BackspaceAndDeleteRemoveTheCharacterEitherSideOfTheCaret()
    {
        var editor = Typed("ABCD");
        editor.MoveTo(2, extend: false);

        editor.Press(Keys.Back, shift: false);
        Assert.Equal("ACD", editor.Text);
        Assert.Equal(1, editor.Caret);
        editor.Press(Keys.Delete, shift: false);
        Assert.Equal("AD", editor.Text);
        Assert.Equal(1, editor.Caret);
        editor.MoveTo(0, extend: false);
        Assert.False(editor.Backspace());
        editor.MoveTo(2, extend: false);
        Assert.False(editor.Delete());
        Assert.Equal("AD", editor.Text);
    }

    [Fact]
    public void ShiftExtendsASelectionThatTypingAndDeletingReplace()
    {
        var editor = Typed("ABCDE");
        editor.Press(Keys.Home, shift: false);
        editor.Press(Keys.Right, shift: false);
        editor.Press(Keys.Right, shift: true);
        editor.Press(Keys.Right, shift: true);

        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionEnd);
        editor.Type('x');
        Assert.Equal("AXDE", editor.Text);
        Assert.False(editor.HasSelection);
        Assert.Equal(2, editor.Caret);

        editor.Press(Keys.End, shift: true);
        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(4, editor.SelectionEnd);
        editor.Press(Keys.Back, shift: false);
        Assert.Equal("AX", editor.Text);

        editor.Press(Keys.Home, shift: true);
        editor.Press(Keys.Delete, shift: false);
        Assert.Equal(string.Empty, editor.Text);

        editor = Typed("ABC");
        editor.Press(Keys.Left, shift: true);
        editor.Press(Keys.Left, shift: false);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void TextRunsPastTheTenCharactersANameKeepsUpToTheControlsDefaultLimit()
    {
        var editor = Typed("ABCDEFGHIJKL");
        Assert.Equal("ABCDEFGHIJKL", editor.Text);

        var full = new SetupPlayerNameEditor();
        full.Begin(new string('A', SetupPlayerNameEditor.TextLimit));
        full.MoveTo(5, extend: false);
        Assert.False(full.Type('B'));
        Assert.Equal(32_767, full.Text.Length);
        Assert.Throws<ArgumentException>(() => full.Begin(new string('A', 32_768)));
    }

    [Fact]
    public void OkKeepsTheFirstTenCharactersWithThoseOutsideSpaceToZAsSpaces()
    {
        Assert.Equal("ABCDEFGHIJ", LocalSetupPolicy.NameAfterModalEntry("OLD", "ABCDEFGHIJKL"));
        Assert.Equal("A C E   G ", LocalSetupPolicy.NameAfterModalEntry("OLD", "A^C_EÉ[\\G~"));
        Assert.Equal("  ", LocalSetupPolicy.NameAfterModalEntry("OLD", "_^"));
        Assert.Equal("ZAB", LocalSetupPolicy.NameAfterModalEntry("OLD", "ZAB"));

        // Typing past ten characters, then deleting at the start, brings later ones into the name.
        var editor = Typed("ABCDEFGHIJKL");
        editor.Press(Keys.Home, shift: false);
        editor.Press(Keys.Delete, shift: false);
        editor.Press(Keys.Delete, shift: false);
        Assert.Equal("CDEFGHIJKL", LocalSetupPolicy.NameAfterModalEntry("OLD", editor.Text));
    }

    [Fact]
    public void CardShowsTenCharactersScrolledToKeepTheCaretInView()
    {
        var editor = Typed("ABCDEFGHIJKL");
        Assert.Equal(2, editor.FirstVisible);
        Assert.Equal("CDEFGHIJKL", editor.VisibleText);

        editor.Press(Keys.Home, shift: false);
        Assert.Equal(0, editor.FirstVisible);
        editor.MoveTo(10, extend: false);
        Assert.Equal(0, editor.FirstVisible);
        editor.Press(Keys.Right, shift: false);
        Assert.Equal(1, editor.FirstVisible);
        editor.Press(Keys.End, shift: false);
        Assert.Equal(2, editor.FirstVisible);
        editor.Press(Keys.Back, shift: false);
        editor.Press(Keys.Back, shift: false);
        Assert.Equal(0, editor.FirstVisible);
        Assert.Equal("ABCDEFGHIJ", editor.VisibleText);
    }

    [Fact]
    public void APressPlacesTheCaretAtTheNearestCellBoundary()
    {
        var editor = Typed("ABCD");

        Assert.Equal(0, editor.IndexAt(-4, 6));
        Assert.Equal(0, editor.IndexAt(2, 6));
        Assert.Equal(1, editor.IndexAt(3, 6));
        Assert.Equal(2, editor.IndexAt(13, 6));
        Assert.Equal(4, editor.IndexAt(40, 6));

        var scrolled = Typed("ABCDEFGHIJKL");
        Assert.Equal(2, scrolled.IndexAt(0, 6));
        Assert.Equal(12, scrolled.IndexAt(60, 6));
    }

    [Fact]
    public void CaretSitsLeftOfItsCellAndBlinksFromWhenItWasPlaced()
    {
        // FND-SETUP-014: a four-character name on card 0 starts at x 385 + 45 - 12, row 95 + 58.
        Assert.Equal(new Point(418, 153), SetupPlayerCardArtLayout.NameStart(0, 4));
        Assert.Equal(new Rectangle(417, 152, 1, 9), SetupPlayerCardArtLayout.NameCaret(0, 4, 0));
        Assert.Equal(new Rectangle(429, 152, 1, 9), SetupPlayerCardArtLayout.NameCaret(0, 4, 2));
        Assert.Equal(new Rectangle(424, 152, 12, 9), SetupPlayerCardArtLayout.NameCells(0, 4, 1, 2));

        Assert.True(SetupPlayerCardArtLayout.NameCaretShown(TimeSpan.Zero));
        Assert.True(SetupPlayerCardArtLayout.NameCaretShown(TimeSpan.FromMilliseconds(529)));
        Assert.False(SetupPlayerCardArtLayout.NameCaretShown(TimeSpan.FromMilliseconds(530)));
        Assert.True(SetupPlayerCardArtLayout.NameCaretShown(TimeSpan.FromMilliseconds(1060)));
    }
}

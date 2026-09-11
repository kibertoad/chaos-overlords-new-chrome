using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class LocalSetupUiTests
{
    [Fact]
    public void LocalSetupStartsWithOneHumanAndOriginalDefaultNames()
    {
        Assert.Equal(1, LocalSetupPolicy.DefaultHumanPlayerCount);
        Assert.Equal("PLAYER#1", LocalSetupPolicy.DefaultPlayerName(0));
        Assert.Equal("PLAYER#6", LocalSetupPolicy.DefaultPlayerName(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocalSetupPolicy.DefaultPlayerName(6));
        Assert.Equal(15, PlayerPortraitLayout.SelectableCount);
        Assert.Equal(16, PlayerPortraitLayout.Count);
    }

    [Fact]
    public void NameEditorUsesOriginalTenCharacterUppercaseField()
    {
        var editor = new SetupPlayerNameEditor();
        editor.Begin("Player#1");

        Assert.Equal("PLAYER#1", editor.Text);
        Assert.True(editor.TryAppend('a'));
        Assert.True(editor.TryAppend('b'));
        Assert.True(editor.IsFull);
        Assert.False(editor.TryAppend('C'));
        Assert.False(editor.TryAppend('~'));
        Assert.True(editor.Backspace());
        Assert.Equal("PLAYER#1A", editor.Text);
        Assert.Throws<ArgumentException>(() => editor.Begin("ELEVEN CHARS"));
    }

    [Fact]
    public void NameHitRegionsFollowEachRecoveredPortraitCell()
    {
        Assert.Equal(new Rectangle(379, 149, 64, 12), PlayerPortraitLayout.Name(0));
        Assert.Equal(new Rectangle(485, 333, 64, 12), PlayerPortraitLayout.Name(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPortraitLayout.Name(6));
    }

    [Fact]
    public void SharedOriginalTextInputMapsLettersDigitsAndPunctuation()
    {
        Assert.True(OriginalTextInput.TryCharacter(Keys.A, shift: false, out var letter));
        Assert.Equal('A', letter);
        Assert.True(OriginalTextInput.TryCharacter(Keys.D1, shift: true, out var bang));
        Assert.Equal('!', bang);
        Assert.True(OriginalTextInput.TryCharacter(Keys.OemQuestion, shift: false, out var slash));
        Assert.Equal('/', slash);
        Assert.False(OriginalTextInput.TryCharacter(Keys.F1, shift: false, out _));
    }
}

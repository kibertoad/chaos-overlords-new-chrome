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
        Assert.Equal(new Rectangle(397, 153, 64, 8), PlayerPortraitLayout.Name(0));
        Assert.Equal(new Rectangle(480, 301, 64, 8), PlayerPortraitLayout.Name(5));
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

    [Fact]
    public void HumanRosterAddsRemovesMovesAndExchangesColorSlots()
    {
        var roster = new LocalSetupRoster();

        Assert.Equal([0], roster.HumanSlots);
        Assert.Equal(1, roster.AddHuman());
        Assert.Equal(2, roster.AddHuman());
        Assert.Equal(LocalSetupMoveResult.ExchangedHumanColors, roster.MoveHuman(0, 2));
        Assert.Equal([0, 1, 2], roster.HumanSlots);
        Assert.Equal(LocalSetupMoveResult.MovedToEmptyColor, roster.MoveHuman(0, 5));
        Assert.Equal([5, 1, 2], roster.HumanSlots);
        Assert.Equal(2, roster.RemoveLastHuman());
        Assert.Equal([5, 1], roster.HumanSlots);
        Assert.Equal(LocalSetupMoveResult.Invalid, roster.MoveHuman(0, 3));
    }
}

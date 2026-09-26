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

    // RULE-SETUP-009: the arrows skip the portraits any human slot holds, wrapping at both ends.
    [Fact]
    public void PortraitArrowsSkipPortraitsOtherHumansHold()
    {
        short[] portraits = [0, 1, 2, 3, 4, 5];
        int[] humans = [0, 1, 2];

        Assert.Equal(3, LocalSetupPolicy.StepPortrait(portraits, humans, 0, 1));
        Assert.Equal(14, LocalSetupPolicy.StepPortrait(portraits, humans, 0, -1));
        Assert.Equal(3, LocalSetupPolicy.StepPortrait(portraits, humans, 2, 1));
        portraits[0] = 14;
        Assert.Equal(3, LocalSetupPolicy.StepPortrait(portraits, humans, 1, 1));
        Assert.Equal(0, LocalSetupPolicy.StepPortrait(portraits, humans, 0, 1));
        // Slots 3 to 5 are empty, so their portraits are free.
        Assert.Equal(13, LocalSetupPolicy.StepPortrait(portraits, humans, 0, -1));
    }

    // RULE-SETUP-010: Add gives the lowest portrait no human holds.
    [Fact]
    public void AddGivesTheLowestFreePortrait()
    {
        short[] portraits = [1, 0, 2, 3, 4, 5];

        Assert.Equal(0, LocalSetupPolicy.LowestFreePortrait(portraits, [0]));
        Assert.Equal(2, LocalSetupPolicy.LowestFreePortrait(portraits, [0, 1]));
        Assert.Equal(3, LocalSetupPolicy.LowestFreePortrait(portraits, [0, 1, 2]));
    }

    // RULE-SETUP-010: the first setup has one human in slot 0 with portrait 0, and a reopened setup
    // takes the saved humans.
    [Fact]
    public void RosterRestoresTheSavedHumans()
    {
        Assert.Equal([0], LocalSetupSnapshot.Initial.HumanSlots);
        Assert.Equal(0, LocalSetupSnapshot.Initial.Portraits[0]);
        Assert.Equal("PLAYER#3", LocalSetupSnapshot.Initial.Names[2]);

        var roster = new LocalSetupRoster();
        roster.Restore([4, 1]);

        Assert.Equal([4, 1], roster.HumanSlots);
        Assert.Throws<ArgumentOutOfRangeException>(() => roster.Restore([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => roster.Restore([6]));
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
    public void EmptyAcceptedModalNameKeepsTheExistingNativeRecord()
    {
        Assert.Equal("ORIGINAL", LocalSetupPolicy.NameAfterModalEntry("ORIGINAL", string.Empty));
        Assert.Equal(" ", LocalSetupPolicy.NameAfterModalEntry("ORIGINAL", " "));
        Assert.Equal("NEW", LocalSetupPolicy.NameAfterModalEntry("ORIGINAL", "NEW"));
    }

    [Fact]
    public void SetupHitRegionsFollowTheNativeInteractionCells()
    {
        Assert.Equal(new Rectangle(397, 94, 64, 68), PlayerPortraitLayout.SetupHit(0));
        Assert.Equal(new Rectangle(480, 242, 64, 68), PlayerPortraitLayout.SetupHit(5));
        Assert.Equal(new Rectangle(397, 94, 16, 58), PlayerPortraitLayout.PreviousHit(0));
        Assert.Equal(new Rectangle(446, 94, 15, 58), PlayerPortraitLayout.NextHit(0));
        Assert.Equal(new Rectangle(397, 152, 64, 10), PlayerPortraitLayout.NameHit(0));
        Assert.Equal(new Rectangle(480, 300, 64, 10), PlayerPortraitLayout.NameHit(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPortraitLayout.NameHit(6));
    }

    [Fact]
    public void SetupDragUsesTheNativeFourPixelBoxAndFortyPixelToken()
    {
        var pressed = new Point(400, 100);
        Assert.False(PlayerPortraitLayout.SetupDragMoved(pressed, pressed));
        Assert.False(PlayerPortraitLayout.SetupDragMoved(pressed, new Point(398, 98)));
        Assert.True(PlayerPortraitLayout.SetupDragMoved(pressed, new Point(402, 100)));
        Assert.True(PlayerPortraitLayout.SetupDragMoved(pressed, new Point(397, 100)));
        Assert.Equal(new Rectangle(0, 0, 40, 40),
            PlayerPortraitLayout.SetupDragToken(Point.Zero));
        Assert.Equal(new Rectangle(600, 420, 40, 40),
            PlayerPortraitLayout.SetupDragToken(new Point(639, 459)));
    }

    [Fact]
    public void FirstClickSelectsAnotherCardBeforeItsSubcontrolsAct()
    {
        var nextOnPlayerOne = new Point(533, 100);

        Assert.Equal(SetupPlayerCardClick.Select,
            PlayerPortraitLayout.ClickAction(0, 1, nextOnPlayerOne));
        Assert.Equal(SetupPlayerCardClick.NextPortrait,
            PlayerPortraitLayout.ClickAction(1, 1, nextOnPlayerOne));
        Assert.Equal(SetupPlayerCardClick.EditName,
            PlayerPortraitLayout.ClickAction(1, 1, new Point(490, 155)));
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
        Assert.Equal(2, roster.RemoveHuman(2));
        Assert.Equal([5, 1], roster.HumanSlots);
        Assert.Equal(LocalSetupMoveResult.Invalid, roster.MoveHuman(0, 3));
        Assert.Equal(1, roster.RemoveHuman(1));
        Assert.Null(roster.RemoveHuman(5));
    }
}

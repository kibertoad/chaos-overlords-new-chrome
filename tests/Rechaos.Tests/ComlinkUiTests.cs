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
        Assert.Equal(new Rectangle(104, 125, 344, 209), ComlinkViewLayout.Panel);
        Assert.Equal(ComlinkViewLayout.Panel, ComlinkSendLayout.Panel);
        Assert.Equal(new Rectangle(196, 251, 240, 36), ComlinkViewLayout.Message);
        Assert.Equal(new Rectangle(196, 258, 240, 36), ComlinkSendLayout.Message);
        Assert.Equal(new Rectangle(196, 143, 56, 32), ComlinkSendLayout.Recipient(0));
        Assert.Equal(new Rectangle(324, 209, 56, 32), ComlinkSendLayout.Recipient(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComlinkSendLayout.Recipient(6));
    }

    [Fact]
    public void EditorUsesFourRecoveredFortyCharacterRows()
    {
        var editor = new ComlinkTextEditor();
        for (var index = 0; index < MatchLimits.ComlinkMessageCharacters; index++)
            Assert.True(editor.TryAppend((char)('a' + index % 26)));

        Assert.True(editor.IsFull);
        Assert.False(editor.TryAppend('X'));
        Assert.All(editor.DisplayLines(), line => Assert.Equal(40, line.Length));
        Assert.StartsWith("ABC", editor.Text);
        Assert.True(editor.Backspace());
        Assert.False(editor.IsFull);
        Assert.True(editor.TryAppend('?'));
        Assert.False(editor.TryAppend('~'));
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
}

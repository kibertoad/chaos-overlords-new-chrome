using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ClassicOnlineLobbyLayoutTests
{
    [Fact]
    public void MapsModernOperationsOntoDistinctOriginalLobbyFaces()
    {
        Rectangle[] controls =
        [
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
            ClassicOnlineLobbyLayout.Leave
        ];

        Assert.All(controls, control => Assert.InRange(control.Left, 0, 639));
        Assert.All(controls, control => Assert.InRange(control.Top, 0, 459));
        Assert.All(controls, control => Assert.True(control.Right <= 640 && control.Bottom <= 460));
        Assert.False(ClassicOnlineLobbyLayout.Start.Intersects(ClassicOnlineLobbyLayout.Leave));
        Assert.False(ClassicOnlineLobbyLayout.PublicChoice.Intersects(ClassicOnlineLobbyLayout.PrivateChoice));
        Assert.False(ClassicOnlineLobbyLayout.LateJoinAllowed.Intersects(ClassicOnlineLobbyLayout.LateJoinRefused));
        // Every face is its own: no two controls share a pixel, so a click reaches exactly one.
        for (var first = 0; first < controls.Length; first++)
            for (var second = first + 1; second < controls.Length; second++)
                Assert.False(controls[first].Intersects(controls[second]),
                    $"{controls[first]} overlaps {controls[second]}");
        // The longest label the watch face takes fits it.
        Assert.True($"WATCH: {SpectatorDelayChoice.Maximum} LATE".Length * OriginalFontLayout.CellWidth
            <= ClassicOnlineLobbyLayout.WatchAllowed.Width);
    }

    [Fact]
    public void ShowsAllSixModernSeatsInsideTheClassicRosterWell()
    {
        var rows = Enumerable.Range(0, 6).Select(ClassicOnlineLobbyLayout.RosterPortrait).ToArray();

        Assert.All(rows, row => Assert.True(ClassicOnlineLobbyLayout.Roster.Contains(row)));
        Assert.All(rows.Zip(rows.Skip(1)), pair => Assert.True(pair.First.Bottom <= pair.Second.Y));
    }

    [Fact]
    public void EachRosterNameSitsBesideItsFaceInsideTheWell()
    {
        for (var row = 0; row < 6; row++)
        {
            var name = ClassicOnlineLobbyLayout.RosterName(row);
            var face = ClassicOnlineLobbyLayout.RosterPortrait(row);
            Assert.True(ClassicOnlineLobbyLayout.Roster.Contains(name));
            Assert.False(name.Intersects(face));
            Assert.Equal(face.Y, name.Y);
        }
    }
}

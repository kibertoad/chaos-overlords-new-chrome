using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The Escape menu and the panels it opens, held to the original 640x460 interface.
/// </summary>
public sealed class GameMenuLayoutTests
{
    [Fact]
    public void GameMenuProvidesSaveLoadOptionsReportBugAndConfirmedMainMenuExit()
    {
        Assert.Equal(new Rectangle(226, 162, 188, 32), GameMenuLayout.Save);
        Assert.Equal(new Rectangle(226, 200, 188, 32), GameMenuLayout.Load);
        Assert.Equal(new Rectangle(226, 238, 188, 32), GameMenuLayout.Options);
        Assert.Equal(new Rectangle(226, 276, 188, 32), GameMenuLayout.ReportBug);
        Assert.Equal(new Rectangle(226, 314, 188, 42), GameMenuLayout.QuitToMainMenu);
        Assert.Equal(9, SaveSlotCatalog.SlotCount);
        Assert.Equal(new Rectangle(58, 344, 524, 35), GameMenuLayout.SlotRow(8));
        Assert.True(GameMenuLayout.Panel.Contains(GameMenuLayout.Resume));
        Assert.True(GameMenuLayout.Panel.Contains(GameMenuLayout.ReportBug));
        Assert.True(GameMenuLayout.Panel.Contains(GameMenuLayout.ConfirmQuit));
        Assert.True(GameMenuLayout.Panel.Contains(GameMenuLayout.CancelQuit));
    }

    /// <summary>Five entries, none of them overlapping, all of them inside the panel.</summary>
    [Fact]
    public void EveryGameMenuEntryHasItsOwnPlaceInThePanel()
    {
        Rectangle[] entries =
        [
            GameMenuLayout.Resume, GameMenuLayout.Save, GameMenuLayout.Load,
            GameMenuLayout.Options, GameMenuLayout.ReportBug, GameMenuLayout.QuitToMainMenu
        ];

        Assert.Equal(GameMenuLayout.EntryCount, entries.Length);
        Assert.Equal(GameMenuLayout.Options, entries[GameMenuLayout.OptionsIndex]);
        Assert.Equal(GameMenuLayout.ReportBug, entries[GameMenuLayout.ReportBugIndex]);
        foreach (var entry in entries) Assert.True(GameMenuLayout.Panel.Contains(entry));
        for (var index = 1; index < entries.Length; index++)
            Assert.False(entries[index].Intersects(entries[index - 1]));
    }

    /// <summary>
    /// An online match's join code and password grow the panel instead of spilling out of it, and
    /// land clear of the last button.
    /// </summary>
    [Fact]
    public void SessionLinesSitUnderTheMenuButtonsInsideAPanelThatFitsThem()
    {
        Assert.Equal(new Rectangle(176, 68, 288, 310), GameMenuLayout.Panel);
        Assert.Equal(GameMenuLayout.Panel, GameMenuLayout.PanelWith(lines: 0, columns: 0));

        // "PASSWORD  " and the longest password the connect screen accepts.
        var panel = GameMenuLayout.PanelWith(lines: 2, columns: 74);

        Assert.True(new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height).Contains(panel));
        Assert.True(panel.Contains(GameMenuLayout.QuitToMainMenu));
        Assert.Equal(VirtualInput.Width / 2, panel.Center.X);
        Assert.True(74 * 6 <= panel.Width - 16, $"{panel.Width} is too narrow for 74 columns");
        Assert.True(GameMenuLayout.SessionLine(0) > GameMenuLayout.QuitToMainMenu.Bottom);
        Assert.True(GameMenuLayout.SessionLine(1) > GameMenuLayout.SessionLine(0));
        Assert.True(GameMenuLayout.SessionLine(1) + 7 < panel.Bottom);
        Assert.True(GameMenuLayout.PanelWith(lines: 1, columns: 20).Height < panel.Height);
    }

    /// <summary>
    /// The bug report panel fits the original 640x460 interface, and its controls do not collide.
    /// </summary>
    [Fact]
    public void BugReportPanelFitsTheInterfaceWithoutOverlappingControls()
    {
        Rectangle[] controls =
        [
            BugReportLayout.Message, BugReportLayout.ShareStateRow,
            BugReportLayout.Send, BugReportLayout.Cancel
        ];

        Assert.True(new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height)
            .Contains(BugReportLayout.Panel));
        foreach (var control in controls)
            Assert.True(BugReportLayout.Panel.Contains(control), $"{control} escapes the panel");
        Assert.True(BugReportLayout.ShareStateRow.Contains(BugReportLayout.ShareStateBox));
        Assert.False(BugReportLayout.Send.Intersects(BugReportLayout.Cancel));
        Assert.Equal(BugReportLayout.Cancel, BugReportLayout.Ok);
        Assert.False(BugReportLayout.Message.Intersects(BugReportLayout.ShareStateRow));
        // The message box has to hold the lines it says it does, at the original 6x7 font cell.
        Assert.True(BugReportLayout.MessageColumns * 6 <= BugReportLayout.Message.Width - 10);
        Assert.True(BugReportLayout.MessageRows * 9 <= BugReportLayout.Message.Height - 5);
    }
}

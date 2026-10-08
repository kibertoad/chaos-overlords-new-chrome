using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class UiNavigationTests
{
    /// <summary>Left and Right flip settings, but never run the diagnostics export.</summary>
    [Fact]
    public void OptionsArrowsNeverExportDiagnostics()
    {
        Assert.Equal(OptionsLayout.ExportDiagnostics,
            OptionsLayout.ToggleRows[OptionsLayout.ExportDiagnosticsRow - OptionsLayout.FirstToggleRow]);
        Assert.Equal(OptionsLayout.LastRow, OptionsLayout.FirstToggleRow + OptionsLayout.ToggleRows.Count - 1);
        Assert.False(OptionsLayout.ArrowsToggle(OptionsLayout.ExportDiagnosticsRow));
        Assert.False(OptionsLayout.ArrowsToggle(0));
        Assert.False(OptionsLayout.ArrowsToggle(1));
        Assert.All(
            Enumerable.Range(OptionsLayout.FirstToggleRow, OptionsLayout.ToggleRows.Count)
                .Where(row => row != OptionsLayout.ExportDiagnosticsRow),
            row => Assert.True(OptionsLayout.ArrowsToggle(row)));
    }

    /// <summary>Each named cursor row is the row its toggle is drawn and hit-tested on.</summary>
    [Fact]
    public void OptionsCursorRowsMatchTheirToggles()
    {
        (int Row, Rectangle Toggle)[] rows =
        [
            (OptionsLayout.BaseStatisticsRow, OptionsLayout.BaseStatistics),
            (OptionsLayout.DetailedCombatRow, OptionsLayout.DetailedCombat),
            (OptionsLayout.SlidePanelsRow, OptionsLayout.SlidePanels),
            (OptionsLayout.SteadyLightsRow, OptionsLayout.SteadyLights),
            (OptionsLayout.WarnIfIdleGangsRow, OptionsLayout.WarnIfIdleGangs),
            (OptionsLayout.EventSiteImagesRow, OptionsLayout.EventSiteImages),
            (OptionsLayout.AdvancedAiRow, OptionsLayout.AdvancedAi),
            (OptionsLayout.IntroOnlyOnceRow, OptionsLayout.IntroOnlyOnce),
            (OptionsLayout.MenuStopsClockRow, OptionsLayout.MenuStopsClock),
            (OptionsLayout.ExportDiagnosticsRow, OptionsLayout.ExportDiagnostics),
            (OptionsLayout.OnlineLobbyPresentationRow, OptionsLayout.OnlineLobbyPresentation),
        ];

        Assert.Equal(OptionsLayout.ToggleRows.Count, rows.Length);
        Assert.All(rows, row => Assert.Equal(
            row.Toggle, OptionsLayout.ToggleRows[row.Row - OptionsLayout.FirstToggleRow]));
    }

    [Fact]
    public void OptionsExposeBothOriginalAudioScalesAsDistinctHitTargets()
    {
        Assert.Equal(OriginalSoundtrackPolicy.MaximumVolumeLevel + 1,
            OptionsLayout.MusicLevels.Count);
        Assert.Equal(OptionsLayout.MusicLevels.Count, OptionsLayout.SoundEffectLevels.Count);
        Assert.Equal(new Rectangle(132, 78, 28, 28), OptionsLayout.MusicLevels[0]);
        Assert.Equal(new Rectangle(472, 78, 28, 28), OptionsLayout.MusicLevels[^1]);
        Assert.Equal(new Rectangle(132, 140, 28, 28), OptionsLayout.SoundEffectLevels[0]);
        Assert.All(OptionsLayout.MusicLevels.Concat(OptionsLayout.SoundEffectLevels),
            level => Assert.True(OptionsLayout.Panel.Contains(level)));
        var levels = OptionsLayout.MusicLevels.Concat(OptionsLayout.SoundEffectLevels).ToArray();
        Assert.All(levels.SelectMany((left, index) =>
                levels.Skip(index + 1).Select(right => (left, right))),
            pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.WarnIfIdleGangs));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.BaseStatistics));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.DetailedCombat));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.SlidePanels));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.SteadyLights));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.EventSiteImages));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.AdvancedAi));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.IntroOnlyOnce));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.MenuStopsClock));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.ExportDiagnostics));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.ColorDepth));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.Done));
    }

    [Fact]
    public void EveryOptionsEntryHasAnExplanatoryHoverTooltip()
    {
        Rectangle[] entries =
        [
            OptionsLayout.Music,
            OptionsLayout.SoundEffects,
            OptionsLayout.BaseStatistics,
            OptionsLayout.DetailedCombat,
            OptionsLayout.SlidePanels,
            OptionsLayout.SteadyLights,
            OptionsLayout.WarnIfIdleGangs,
            OptionsLayout.EventSiteImages,
            OptionsLayout.AdvancedAi,
            OptionsLayout.IntroOnlyOnce,
            OptionsLayout.MenuStopsClock,
            OptionsLayout.ExportDiagnostics,
            OptionsLayout.ColorDepth,
            OptionsLayout.Done
        ];

        Assert.All(entries, entry =>
        {
            var lines = OptionsTooltip.At(entry.Center);
            Assert.True(lines.Count >= 2);
            var bounds = OptionsTooltip.Bounds(entry.Center, lines);
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0);
            Assert.True(bounds.Right <= VirtualInput.Width);
            Assert.True(bounds.Bottom <= VirtualInput.Height);
        });
        Assert.Contains("ORIGINAL HOST-LOBBY ART",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.ColorDepth.Center)));
        Assert.Contains("OFF BLINKS THEM AS THE ORIGINAL DOES",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.SteadyLights.Center)));
        // The rows and the Done face do not overlap, so each press reaches one of them.
        var rows = OptionsLayout.ToggleRows.Append(OptionsLayout.Done).ToArray();
        Assert.All(rows.SelectMany((first, index) => rows.Skip(index + 1).Select(second => (first, second))),
            pair => Assert.False(pair.first.Intersects(pair.second)));
        Assert.Contains("SLIDES PANELS IN FROM THE RIGHT",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.SlidePanels.Center)));
        Assert.Contains("NATIVE STRETCH AND ORDERED DITHER",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.EventSiteImages.Center)));
        Assert.Contains("DOES NOT CHANGE A MATCH ALREADY IN PROGRESS",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.AdvancedAi.Center)));
        Assert.Contains("FALLBACK COMMANDS FOR GANGS ORIGINAL AI LEAVES IDLE",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.AdvancedAi.Center)));
        var diagnosticsTooltip = string.Join(' ',
            OptionsTooltip.At(OptionsLayout.ExportDiagnostics.Center));
        Assert.Contains("DOES NOT INCLUDE REPLAYABLE MATCH STATE", diagnosticsTooltip);
        Assert.Contains("REPORT BUG", diagnosticsTooltip);
        Assert.Contains("CLIENT PROBLEMS A MATCH REPLAY CANNOT SHOW", diagnosticsTooltip);
        Assert.Empty(OptionsTooltip.At(Point.Zero));
        Assert.Equal(Rectangle.Empty, OptionsTooltip.Bounds(Point.Zero, []));
    }
}

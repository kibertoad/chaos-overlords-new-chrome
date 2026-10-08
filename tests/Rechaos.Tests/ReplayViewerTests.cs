using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>The transport and panel of the F10 replay viewer (DEV-UI-026).</summary>
public sealed class ReplayViewerTests
{
    private static readonly Lazy<byte[]> ComputerJournal = new(() =>
        MatchReplayPlaybackTests.Journal(MatchReplayPlaybackTests.RecordComputerMatch(4)));

    [Fact]
    public void PlayingShowsOneBoardChangingStepPerIntervalAndCarriesBookkeepingAlong()
    {
        var viewer = OpenViewer();
        viewer.Execute(ReplayViewerCommand.TogglePlay);
        var interval = TimeSpan.FromSeconds(ReplayViewer.SecondsPerShownStep / viewer.Speed);

        Assert.False(viewer.Advance(interval * 0.9));
        for (var tick = 0; tick < 20 && !viewer.AtEnd; tick++)
        {
            var before = viewer.Playback.Position;
            Assert.True(viewer.Advance(interval * (tick == 0 ? 0.2 : 1)));
            var after = viewer.Playback.Position;
            // Every step passed over on the way is one the board does not draw.
            for (var position = before + 1; position < after; position++)
            {
                viewer.Playback.Seek(position);
                Assert.False(ReplayViewer.IsShownStep(viewer.Playback.CurrentStep!.Kind));
            }
            viewer.Playback.Seek(after);
            Assert.True(viewer.AtEnd || ReplayViewer.IsShownStep(viewer.Playback.CurrentStep!.Kind));
        }
    }

    [Fact]
    public void PlayingStopsAtTheEndAndPlayAgainStartsOver()
    {
        var viewer = OpenViewer();
        viewer.Execute(ReplayViewerCommand.TogglePlay);
        for (var guard = 0; guard < 10_000 && viewer.Playing; guard++) viewer.Advance(TimeSpan.FromSeconds(1));

        Assert.True(viewer.AtEnd);
        Assert.False(viewer.Playing);
        Assert.Equal("END OF REPLAY", viewer.DescribeStatus());

        viewer.Execute(ReplayViewerCommand.TogglePlay);
        Assert.True(viewer.Playing);
        Assert.Equal(0, viewer.Playback.Position);
    }

    [Fact]
    public void OneUpdateAppliesABoundedNumberOfSteps()
    {
        var viewer = OpenViewer();
        viewer.Execute(ReplayViewerCommand.TogglePlay);
        viewer.Advance(TimeSpan.FromHours(1));
        Assert.InRange(viewer.Playback.Position, 1, ReplayViewer.MaximumStepsPerUpdate);
    }

    [Fact]
    public void NavigationPausesAndStaysInsideTheJournal()
    {
        var viewer = OpenViewer();
        viewer.Execute(ReplayViewerCommand.TogglePlay);

        Assert.False(viewer.Execute(ReplayViewerCommand.PreviousStep));
        Assert.False(viewer.Playing);
        Assert.True(viewer.Execute(ReplayViewerCommand.NextStep));
        Assert.Equal(1, viewer.Playback.Position);
        Assert.True(viewer.Execute(ReplayViewerCommand.End));
        Assert.True(viewer.AtEnd);
        Assert.False(viewer.Execute(ReplayViewerCommand.NextStep));
        Assert.False(viewer.Execute(ReplayViewerCommand.NextTurn));
        Assert.True(viewer.Execute(ReplayViewerCommand.PreviousTurn));
        Assert.True(viewer.Playback.Position < viewer.Playback.StepCount);
        Assert.True(viewer.Execute(ReplayViewerCommand.Start));
        Assert.Equal(0, viewer.Playback.Position);
        Assert.False(viewer.SeekTo(-5));
        Assert.True(viewer.SeekTo(int.MaxValue));
        Assert.True(viewer.AtEnd);
    }

    [Fact]
    public void SpeedStepsThroughItsRangeAndStopsAtEachEnd()
    {
        var viewer = OpenViewer();
        Assert.Equal(1, viewer.Speed);
        for (var press = 0; press < 10; press++) viewer.Execute(ReplayViewerCommand.Faster);
        Assert.Equal(ReplayViewer.Speeds[^1], viewer.Speed);
        Assert.Equal("PAUSED 8X", viewer.DescribeTransport());
        for (var press = 0; press < 10; press++) viewer.Execute(ReplayViewerCommand.Slower);
        Assert.Equal(ReplayViewer.Speeds[0], viewer.Speed);
        Assert.Equal("PAUSED 0.25X", viewer.DescribeTransport());
    }

    [Fact]
    public void TheStatusSaysWhenTheBackupIsShown()
    {
        var viewer = OpenViewer(new ReplayFailure(ReplayFailureKind.Damaged));
        Assert.Equal("SHOWING BACKUP REPLAY", viewer.DescribeStatus());
        Assert.Equal("VERIFIED REPLAY", OpenViewer().DescribeStatus());
    }

    public static TheoryData<ReplayFailure, string> Failures => new()
    {
        { new ReplayFailure(ReplayFailureKind.Missing), "NO REPLAY SAVED  F6 SAVES ONE" },
        { new ReplayFailure(ReplayFailureKind.Incompatible, IncompatibleSaveReason.NewerFormat), "REPLAY FROM A NEWER VERSION" },
        { new ReplayFailure(ReplayFailureKind.Incompatible, IncompatibleSaveReason.OlderFormat), "REPLAY FROM AN OLDER VERSION" },
        { new ReplayFailure(ReplayFailureKind.Incompatible, IncompatibleSaveReason.DifferentDefinitions), "REPLAY USES OTHER GAME DATA" },
        { new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: 11), "REPLAY DIVERGED AT STEP 12" },
        { new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: 999_999), "REPLAY DIVERGED AT STEP 1000000" },
        { new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: -1), "REPLAY START DOES NOT MATCH" },
        { new ReplayFailure(ReplayFailureKind.Damaged), "REPLAY FILE DAMAGED" },
        { new ReplayFailure(ReplayFailureKind.Unreadable), "REPLAY FILE COULD NOT BE READ" }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void EveryFailureHasAMessageThatFitsTheConsole(ReplayFailure failure, string message)
    {
        Assert.Equal(message, ReplayViewer.DescribeFailure(failure));
    }

    [Fact]
    public void AMissingPrimaryBesideAPlayableBackupIsNotCalledAMissingReplay()
    {
        Assert.Equal("LATEST REPLAY FILE MISSING",
            ReplayViewer.DescribePrimaryFailure(new ReplayFailure(ReplayFailureKind.Missing)));
        Assert.Equal("REPLAY DIVERGED AT STEP 3",
            ReplayViewer.DescribePrimaryFailure(new ReplayFailure(ReplayFailureKind.Diverged, DivergedStep: 2)));
        foreach (var row in Failures)
            Assert.InRange(ReplayViewer.DescribePrimaryFailure(row.Data.Item1).Length, 1,
                ReplayControlLayout.TextColumns);
    }

    [Fact]
    public void EveryStepKindHasADescriptionThatFitsThePanel()
    {
        var state = TestMatches.Create(firstPlayerName: "LONGESTNAM");
        foreach (var kind in Enum.GetValues<ReplayOperationKind>())
        {
            var text = ReplayViewer.DescribeStep(
                state, new ReplayStep(kind, string.Empty, Player: new PlayerId(0)), ReplayControlLayout.TextColumns);
            Assert.InRange(text.Length, 1, ReplayControlLayout.TextColumns);
            Assert.DoesNotContain(kind.ToString(), text, StringComparison.Ordinal);
        }
        Assert.Equal("OPENING STATE", ReplayViewer.DescribeStep(state, null, ReplayControlLayout.TextColumns));
    }

    [Fact]
    public void ThePanelCoversTheConsoleControlsButNotTheMapOrTheSectorValues()
    {
        var panel = ReplayControlLayout.Panel;
        Assert.False(panel.Intersects(CityMapLayout.Bounds));
        Assert.True(panel.Top > StatusConsoleLayout.SectorValueY(4) + OriginalFontLayout.GlyphHeight);
        Assert.True(new Rectangle(0, 0, 640, 460).Contains(panel));
        Assert.True(panel.Contains(ReplayControlLayout.Timeline));
        foreach (var help in ReplayControlLayout.KeyHelp)
            Assert.InRange(help.Length, 1, ReplayControlLayout.TextColumns);
    }

    [Fact]
    public void EveryButtonIsInsideThePanelApartFromTheOthersAndHitWhereItIsDrawn()
    {
        var buttons = ReplayControlLayout.Buttons;
        Assert.Equal(Enum.GetValues<ReplayViewerCommand>().Length, buttons.Select(button => button.Command).Distinct().Count());
        foreach (var (bounds, command) in buttons)
        {
            Assert.True(ReplayControlLayout.Panel.Contains(bounds), $"{command} leaves the panel.");
            Assert.False(bounds.Intersects(ReplayControlLayout.Timeline), $"{command} covers the timeline.");
            Assert.Equal(command, ReplayControlLayout.CommandAt(bounds.Center));
            Assert.Equal(command, ReplayControlLayout.CommandAt(bounds.Location));
            Assert.Equal(command, ReplayControlLayout.CommandAt(new Point(bounds.Right - 1, bounds.Bottom - 1)));
            foreach (var playing in new[] { false, true })
                Assert.True(ReplayControlLayout.Label(command, playing).Length * OriginalFontLayout.CellWidth
                    <= bounds.Width - 4, $"{command}'s label does not fit.");
            foreach (var (other, otherCommand) in buttons)
                if (otherCommand != command) Assert.False(bounds.Intersects(other), $"{command} overlaps {otherCommand}.");
        }
        Assert.Null(ReplayControlLayout.CommandAt(new Point(10, 10)));
    }

    [Fact]
    public void TheTimelineMapsItsEndsToTheEndsOfTheJournal()
    {
        var timeline = ReplayControlLayout.Timeline;
        var middle = timeline.Center.Y;
        Assert.Equal(0, ReplayControlLayout.TimelinePositionAt(new Point(timeline.Left, middle), 500));
        Assert.Equal(500, ReplayControlLayout.TimelinePositionAt(new Point(timeline.Right - 1, middle), 500));
        Assert.Null(ReplayControlLayout.TimelinePositionAt(new Point(timeline.Left - 1, middle), 500));
        Assert.Equal(0, ReplayControlLayout.TimelineFill(0, 500));
        Assert.Equal(timeline.Width, ReplayControlLayout.TimelineFill(500, 500));
        Assert.Equal(timeline.Width, ReplayControlLayout.TimelineFill(0, 0));
    }

    private static ReplayViewer OpenViewer(ReplayFailure? primaryFailure = null) => new(
        MatchReplaySerializer.OpenPlayback(new MemoryStream(ComputerJournal.Value), BundledOriginalDataCache.Value),
        primaryFailure);

    private static class BundledOriginalDataCache
    {
        public static readonly Rechaos.Core.Assets.OriginalData Value = Rechaos.Core.Assets.BundledOriginalData.Load();
    }
}

using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackProgramCursorTests
{
    private static readonly string[] Disc = SoundtrackCatalog.ExpectedFileNames.ToArray();

    [Fact]
    public void GameplayAdvancesInOrderAndStopsBeforeTheEndgameTrack()
    {
        // RULE-AUDIO-001, FND-AUDIO-007: normal gameplay has a track-8 end position.
        var cursor = new SoundtrackProgramCursor<string>(Disc);
        cursor.Start(OriginalSoundtrackPolicy.FileNamesFor(OriginalSoundtrackMode.Gameplay));
        var played = Collect(cursor);
        Assert.Equal(Disc.Skip(1).Take(6), played);
        Assert.True(cursor.AtEnd);
        Assert.False(cursor.Advance());
        cursor.Start(OriginalSoundtrackPolicy.FileNamesFor(OriginalSoundtrackMode.Gameplay));
        Assert.Equal("track03.ogg", cursor.Current);
    }

    [Theory]
    [InlineData(OriginalSoundtrackMode.Title)]
    [InlineData(OriginalSoundtrackMode.Gameplay)]
    [InlineData(OriginalSoundtrackMode.Endgame)]
    public void ResumePreservesCurrentTrackAndRemovesTheProgramEndBound(OriginalSoundtrackMode mode)
    {
        // RULE-AUDIO-002, FND-AUDIO-007: the positionless resume continues to disc end.
        var cursor = new SoundtrackProgramCursor<string>(Disc);
        cursor.Start(OriginalSoundtrackPolicy.FileNamesFor(mode));
        var current = cursor.Current;
        cursor.ResumeThroughDiscEnd();
        Assert.Equal(current, cursor.Current);
        Assert.Equal(Disc.SkipWhile(track => track != current), Collect(cursor));
        Assert.Equal("track09.ogg", cursor.Current);
    }

    [Fact]
    public void RefocusingDuringContinuationDoesNotRewindAndRestartRestoresOriginalBounds()
    {
        // RULE-AUDIO-001, RULE-AUDIO-002: each resume keeps position, each selector starts afresh.
        var cursor = new SoundtrackProgramCursor<string>(Disc);
        var title = OriginalSoundtrackPolicy.FileNamesFor(OriginalSoundtrackMode.Title);
        cursor.Start(title);
        cursor.ResumeThroughDiscEnd();
        Assert.True(cursor.Advance());
        Assert.Equal("track03.ogg", cursor.Current);
        cursor.ResumeThroughDiscEnd();
        Assert.Equal(Disc.Skip(1), Collect(cursor));
        cursor.Start(title);
        Assert.Equal("track02.ogg", cursor.Current);
        Assert.True(cursor.AtEnd);
    }

    private static List<string> Collect(SoundtrackProgramCursor<string> cursor)
    {
        var tracks = new List<string>();
        if (cursor.Current is { } current) tracks.Add(current);
        while (cursor.Advance()) tracks.Add(cursor.Current!);
        return tracks;
    }
}

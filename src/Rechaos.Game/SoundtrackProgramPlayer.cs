using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

/// <summary>RULE-AUDIO-001, RULE-AUDIO-002: the game thread owns track advancement.
/// DesktopGL finishes songs on its Ogg streaming worker; a one-song native queue
/// prevents that worker from choosing the next track while the game restarts a program.</summary>
public sealed class SoundtrackProgramPlayer : IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly SoundtrackProgramCursor<Song> _cursor;
    private Song? _expectedSong;
    private int _songCompleted;

    public SoundtrackProgramPlayer(IReadOnlyList<Song> orderedDisc)
    {
        _cursor = new SoundtrackProgramCursor<Song>(orderedDisc);
        MediaPlayer.ActiveSongChanged += OnActiveSongChanged;
    }

    public bool ReadyToRestart => MediaPlayer.State == MediaState.Stopped
        && (_expectedSong is null || (Volatile.Read(ref _songCompleted) != 0 && _cursor.AtEnd));

    public void PlayProgram(IReadOnlyList<Song> program)
    {
        CheckOwnerThread();
        Stop();
        _cursor.Start(program);
        PlayCurrent();
    }

    public bool AdvanceTrack()
    {
        CheckOwnerThread();
        if (MediaPlayer.State != MediaState.Stopped || _cursor.AtEnd
            || Interlocked.Exchange(ref _songCompleted, 0) == 0) return false;
        _cursor.Advance();
        PlayCurrent();
        return true;
    }

    public void ResumeThroughDiscEnd()
    {
        CheckOwnerThread();
        if (MediaPlayer.State != MediaState.Paused) return;
        _cursor.ResumeThroughDiscEnd();
        // RULE-AUDIO-002: retain the paused position; only its eventual end bound changes.
        MediaPlayer.Resume();
    }

    public void Stop()
    {
        CheckOwnerThread();
        Volatile.Write(ref _expectedSong, null);
        Interlocked.Exchange(ref _songCompleted, 0);
        if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
    }

    private void PlayCurrent()
    {
        if (_cursor.Current is not { } song) return;
        Volatile.Write(ref _expectedSong, song);
        Interlocked.Exchange(ref _songCompleted, 0);
        // The native completion path now only stops this one song and reports completion.
        MediaPlayer.Play(song);
    }

    private void OnActiveSongChanged(object? sender, EventArgs args)
    {
        // No player mutation on the streaming worker. Waiting for this notification also avoids
        // treating the worker's pre-notification Stopped state as a completed whole program.
        var expected = Volatile.Read(ref _expectedSong);
        // A one-song queue has no intermediate-song change. Avoid reading the native queue
        // here: the owner may already be replacing it by the time this callback returns.
        if (expected is not null && MediaPlayer.State == MediaState.Stopped)
            Interlocked.Exchange(ref _songCompleted, 1);
    }

    private void CheckOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Soundtrack playback must run on its owner thread.");
    }

    public void Dispose()
    {
        Stop();
        MediaPlayer.ActiveSongChanged -= OnActiveSongChanged;
    }
}

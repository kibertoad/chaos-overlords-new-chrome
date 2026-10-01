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
    private bool _restartCurrentOnResume;

    public SoundtrackProgramPlayer(IReadOnlyList<Song> orderedDisc)
    {
        DesktopGlSoundtrackStreaming.EnsureInitialized();
        _cursor = new SoundtrackProgramCursor<Song>(orderedDisc);
        MediaPlayer.ActiveSongChanged += OnActiveSongChanged;
    }

    public bool ReadyToRestart => MediaPlayer.State == MediaState.Paused
        || (MediaPlayer.State == MediaState.Stopped
            && (_expectedSong is null || (Volatile.Read(ref _songCompleted) != 0 && _cursor.AtEnd)));

    public void PlayProgram(IReadOnlyList<Song> program)
    {
        CheckOwnerThread();
        // A worker restart joins the old worker, which may be waiting on the transport lock.
        DesktopGlSoundtrackStreaming.EnsureInitialized();
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        Release();
        _cursor.Start(program);
        PlayCurrent();
    }

    public bool AdvanceTrack()
    {
        CheckOwnerThread();
        DesktopGlSoundtrackStreaming.EnsureInitialized();
        if (MediaPlayer.State != MediaState.Stopped || _cursor.AtEnd || Volatile.Read(ref _songCompleted) == 0) return false;
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        if (MediaPlayer.State != MediaState.Stopped || _cursor.AtEnd
            || Interlocked.Exchange(ref _songCompleted, 0) == 0) return false;
        _cursor.Advance();
        PlayCurrent();
        return true;
    }

    /// <returns>Whether a new track was started, which the caller must watch for a failed start.</returns>
    public bool ResumeThroughDiscEnd()
    {
        CheckOwnerThread();
        DesktopGlSoundtrackStreaming.EnsureInitialized();
        _cursor.ResumeThroughDiscEnd();
        // Completion may still be delivering its Stopped notification. Do not block
        // activation on that callback before it has reported the finished song.
        if (MediaPlayer.State == MediaState.Stopped && !_restartCurrentOnResume) return AdvanceTrack();
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        // RULE-AUDIO-002: activation sends positionless play regardless of device status.
        // A playing or paused track retains its position. Explicit CD stop resets it
        // to the start of the current track; natural completion advances past that track.
        if (MediaPlayer.State == MediaState.Paused)
        {
            MediaPlayer.Resume();
            return false;
        }
        if (MediaPlayer.State != MediaState.Stopped) return false;
        if (!_restartCurrentOnResume) return AdvanceTrack();
        _restartCurrentOnResume = false;
        PlayCurrent();
        return true;
    }

    public void Stop()
    {
        CheckOwnerThread();
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        // RULE-AUDIO-003: the original stop helper sends MCI_STOP only while playing.
        // A paused transport keeps its position for the next activation.
        var state = MediaPlayer.State;
        if (state == MediaState.Paused) return;
        // Between two tracks of a program the native transport is briefly stopped while
        // the original device is still playing, so that gap is stopped like a playing track.
        if (state == MediaState.Stopped && (_expectedSong is null || _cursor.AtEnd)) return;
        Volatile.Write(ref _expectedSong, null);
        // A track whose completion was already reported has handed over to the next one.
        if (Interlocked.Exchange(ref _songCompleted, 0) != 0) _cursor.Advance();
        _restartCurrentOnResume = true;
        if (state == MediaState.Playing) MediaPlayer.Stop();
        // After the native stop: draining the source can select a completion during it.
        DesktopGlSoundtrackStreaming.CancelCompletion();
    }

    /// <summary>Stops the transport whatever its state, so no later activation resumes
    /// or restarts the released track.</summary>
    public void Release()
    {
        CheckOwnerThread();
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        Stop();
        _restartCurrentOnResume = false;
        Volatile.Write(ref _expectedSong, null);
        Interlocked.Exchange(ref _songCompleted, 0);
        if (MediaPlayer.State == MediaState.Paused) MediaPlayer.Stop();
        DesktopGlSoundtrackStreaming.CancelCompletion();
    }

    /// <summary>RULE-AUDIO-003, FND-AUDIO-007: finish at zero, stop, then restore
    /// the volume captured by the fade, whatever the device volume was meanwhile
    /// (level commands do not run during the fade, FND-AUDIO-016).</summary>
    public void FinishFade(float restoredVolume, bool release)
    {
        CheckOwnerThread();
        using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
        try
        {
            MediaPlayer.Volume = 0;
            if (release) Release();
            else Stop();
        }
        finally
        {
            MediaPlayer.Volume = restoredVolume;
        }
    }

    private void PlayCurrent()
    {
        if (_cursor.Current is not { } song) return;
        Volatile.Write(ref _expectedSong, song);
        Interlocked.Exchange(ref _songCompleted, 0);
        DesktopGlSoundtrackStreaming.ResetForNewSong(song);
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
        Release();
        MediaPlayer.ActiveSongChanged -= OnActiveSongChanged;
    }
}

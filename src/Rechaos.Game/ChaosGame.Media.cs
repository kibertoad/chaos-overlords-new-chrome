using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly TimeSpan SoundtrackStartTimeout = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, Song> _soundtrack =
        new(StringComparer.OrdinalIgnoreCase);
    private SongCollection _activeSoundtrack = SongCollection.Empty.Clone();
    private OriginalSoundtrackMode? _soundtrackMode;
    private bool _restartSoundtrackProgram;
    private int _musicVolumeLevel = OriginalSoundtrackPolicy.DefaultVolumeLevel;
    private bool _soundtrackEnabled;
    private bool _soundtrackFailed;
    private bool _soundtrackAwaitingStart;
    private TimeSpan _soundtrackStartDeadline;
    private readonly SoundtrackRestartPoll _soundtrackRestartPoll = new();
    private SoundtrackFade? _soundtrackFade;
    private readonly SoundtrackFocusState _soundtrackFocus = new();
    private SoundtrackProgramPlayer? _soundtrackProgramPlayer;

    private void LoadSoundtrack()
    {
        _soundtrackFailed = false;
        foreach (var path in SoundtrackCatalog.FindAvailableTracks(_assetRoot))
        {
            try
            {
                _soundtrack.Add(Path.GetFileName(path), Song.FromUri(
                    Path.GetFileNameWithoutExtension(path),
                    new Uri(Path.GetFullPath(path), UriKind.Absolute)));
            }
            catch
            {
                DisposeSoundtrack();
                return;
            }
        }

        if (_soundtrack.Count == 0) return;
        try
        {
            MediaPlayer.IsRepeating = false;
            MediaPlayer.IsShuffled = false;
            _soundtrackProgramPlayer = new SoundtrackProgramPlayer(SoundtrackCatalog.ExpectedFileNames
                .Select(fileName => _soundtrack.GetValueOrDefault(fileName))
                .Where(song => song is not null).Cast<Song>().ToArray());
            ApplyAudioVolumeLevels(TimeSpan.Zero);
        }
        catch
        {
            DisposeSoundtrack();
        }
    }

    private void UpdateSoundtrack(GameTime gameTime)
    {
        var poll = _soundtrackFade is null && _soundtrackRestartPoll.Advance(
            _soundtrackEnabled, _soundtrackFocus.WindowActive, _eventPump.Time);
        try
        {
            if (AdvanceSoundtrackFade(gameTime.TotalGameTime)) return;
            if (_soundtrackFailed || _soundtrack.Count == 0 || _introMoviesPlaying || !_soundtrackFocus.WindowActive) return;
            // FND-AUDIO-007: the selector stores the mode even while music is disabled and then
            // plays nothing, so re-enabled music waits for the poll (RULE-AUDIO-003).
            SelectSoundtrackMode(SoundtrackContext(), gameTime.TotalGameTime, _restartSoundtrackProgram);
            _restartSoundtrackProgram = false;
            if (!_soundtrackEnabled || _soundtrackFade is not null || _activeSoundtrack.Count == 0) return;
            if (_soundtrackProgramPlayer?.AdvanceTrack() == true)
            {
                _soundtrackAwaitingStart = true;
                _soundtrackStartDeadline = gameTime.TotalGameTime + SoundtrackStartTimeout;
                return;
            }
            if (MediaPlayer.State == MediaState.Playing)
            {
                _soundtrackAwaitingStart = false;
                return;
            }

            if (_soundtrackAwaitingStart)
            {
                if (gameTime.TotalGameTime < _soundtrackStartDeadline) return;
                DisableSoundtrack();
                return;
            }

            if (poll && _soundtrackProgramPlayer?.ReadyToRestart == true)
                StartSoundtrackProgram(gameTime.TotalGameTime);
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void StartSoundtrackProgram(TimeSpan now)
    {
        if (!_soundtrackEnabled || _activeSoundtrack.Count == 0 || !_soundtrackFocus.WindowActive) return;
        try
        {
            // RULE-AUDIO-001, FND-AUDIO-007: a stopped program restarts at its first track.
            _soundtrackProgramPlayer?.PlayProgram(_activeSoundtrack.ToArray());
            _soundtrackAwaitingStart = true;
            _soundtrackStartDeadline = now + SoundtrackStartTimeout;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    /// <summary>Clears the music for a movie; the first update after playback restarts the
    /// track list the current screen calls for.</summary>
    private void SuspendSoundtrackForIntroMovies()
    {
        if (!_soundtrackEnabled && _soundtrackFade is null) return;
        try
        {
            // The update loop does not run during the movies, so a fade cannot finish there.
            FinishSoundtrackFade();
            if (!_soundtrackEnabled) return;
            _soundtrackMode = null;
            _soundtrackAwaitingStart = false;
            _soundtrackProgramPlayer?.Stop();
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void SelectSoundtrackMode(ClientScreen screen, TimeSpan now, bool restart = false)
    {
        var mode = OriginalSoundtrackPolicy.ModeFor(screen, _eliminationMusicHeld);
        if (_soundtrackMode == mode)
        {
            if (restart) StartSoundtrackProgram(now);
            return;
        }

        // FND-AUDIO-016: game events stay blocked until the fade finishes; the next
        // soundtrack update completes the selector for the pending screen.
        if (MediaPlayer.State == MediaState.Playing)
        {
            BeginSoundtrackFade(now);
            return;
        }
        SetSoundtrackMode(mode, now);
    }

    private void SetSoundtrackMode(OriginalSoundtrackMode mode, TimeSpan now)
    {
        _soundtrackProgramPlayer?.Stop();
        _soundtrackMode = mode;
        _soundtrackAwaitingStart = false;
        _activeSoundtrack = SongCollection.Empty.Clone();
        // RULE-AUDIO-001: retain the complete CD program while advancing its tracks on the game thread.
        foreach (var fileName in OriginalSoundtrackPolicy.FileNamesFor(mode))
            if (_soundtrack.GetValueOrDefault(fileName) is { } song)
                _activeSoundtrack.Add(song);
        StartSoundtrackProgram(now);
    }

    private void BeginSoundtrackFade(TimeSpan now)
    {
        // RULE-AUDIO-001, RULE-AUDIO-003, FND-AUDIO-007: retain integer attenuation,
        // including the final residual volume before the explicit zero and stop.
        _soundtrackFade ??= new SoundtrackFade(MediaPlayer.Volume, now);
        // FND-AUDIO-016: the first write precedes the zero-deadline dispatch.
        MediaPlayer.Volume = _soundtrackFade.FirstStepVolume;
        MediaPlayer.Volume = _soundtrackFade.VolumeAt(now);
        _soundtrackAwaitingStart = false;
    }

    /// <returns>Whether a fade is still running.</returns>
    private bool AdvanceSoundtrackFade(TimeSpan now)
    {
        if (_soundtrackFade is not { } fade) return false;
        MediaPlayer.Volume = fade.VolumeAt(now);
        if (!fade.IsComplete(now)) return true;
        FinishSoundtrackFade();
        return false;
    }

    private void FinishSoundtrackFade()
    {
        if (_soundtrackFade is not { } fade) return;
        _soundtrackFade = null;
        // FND-AUDIO-016: an enabled selector replaces the old program after the fade;
        // muting uses the conditional stop without replacing a paused position.
        _soundtrackProgramPlayer?.FinishFade(fade.RestoredVolume, release: _soundtrackEnabled);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        try
        {
            if (_introMovieAudio?.State == SoundState.Playing) _introMovieAudio.Pause();
        }
        catch (Exception exception)
        {
            FinishIntroMovie("movie.failed", exception);
        }
        if (_soundtrackFocus.Deactivate(_soundtrackFade is not null) && _soundtrackEnabled)
        {
            try
            {
                using var transport = DesktopGlSoundtrackStreaming.SerializeTransport();
                if (MediaPlayer.State == MediaState.Playing)
                {
                    MediaPlayer.Pause();
                }
            }
            catch
            {
                DisableSoundtrack();
            }
        }
        base.OnDeactivated(sender, args);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        base.OnActivated(sender, args);
        try
        {
            if (_introMovieAudio?.State == SoundState.Paused) _introMovieAudio.Resume();
        }
        catch (Exception exception)
        {
            FinishIntroMovie("movie.failed", exception);
        }
        if (!_soundtrackFocus.Activate(_soundtrackFade is not null) || !_soundtrackEnabled) return;
        try
        {
            // RULE-AUDIO-002, FND-AUDIO-007: activation reapplies levels before resuming.
            ApplyAudioVolumeLevels(_inputTime);
            // Background music stays suppressed while the intro movies play.
            if (_introMoviesPlaying || _soundtrackProgramPlayer?.ResumeThroughDiscEnd() != true) return;
            _soundtrackAwaitingStart = true;
            _soundtrackStartDeadline = _inputTime + SoundtrackStartTimeout;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void ApplyAudioVolumeLevels(TimeSpan now)
    {
        var level = _musicVolumeLevel;
        // RULE-AUDIO-003, FND-AUDIO-007: either menu command reapplies effects
        // first and then music, including when the chosen level is unchanged.
        try
        {
            if (_activeEffectVoice is not null)
                _activeEffectVoice.Volume = AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel);
        }
        catch
        {
            // An effect voice failure must not disable the independent music path.
        }
        if (_soundtrack.Count == 0 || _soundtrackFailed) return;
        try
        {
            if (level == 0)
            {
                _soundtrackEnabled = false;
                _soundtrackAwaitingStart = false;
                if (MediaPlayer.State == MediaState.Playing) BeginSoundtrackFade(now);
                else if (_soundtrackFade is null)
                    _soundtrackProgramPlayer?.Stop();
                return;
            }

            // RULE-AUDIO-003, FND-AUDIO-016: a level applies when the full game event
            // handler runs; the window-only fade pump does not enter that handler.
            MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(level);
            // RULE-AUDIO-003: enabling music leaves playback to the next poll.
            _soundtrackEnabled = true;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void DisableSoundtrack()
    {
        _soundtrackFailed = true;
        _soundtrackFade = null;
        _soundtrackEnabled = false;
        _soundtrackAwaitingStart = false;
        try
        {
            _soundtrackProgramPlayer?.Release();
        }
        catch
        {
            // Music is optional presentation; backend failure must not stop play.
        }
    }

    private ClientScreen SoundtrackContext() => _screens.Current switch
    {
        ClientScreen.Options => _optionsReturnScreen,
        ClientScreen.Help => _helpReturnScreen,
        _ => _screens.Current
    };

    private void DisposeSoundtrack()
    {
        DisableSoundtrack();
        try
        {
            _soundtrackProgramPlayer?.Dispose();
        }
        finally
        {
            // Join streaming work before disposing native songs or the audio device.
            DesktopGlSoundtrackStreaming.Shutdown();
        }
        _soundtrackProgramPlayer = null;
        foreach (var song in _soundtrack.Values) song.Dispose();
        _soundtrack.Clear();
        _activeSoundtrack.Clear();
        _soundtrackMode = null;
    }

    protected override void UnloadContent()
    {
        // Do not let the process exit between a completed turn and its rolling snapshot reaching
        // disk. This runs only during shutdown; frame-time work remains on the background worker.
        FlushAutoSaves();
        FlushScenarioPreference(force: true);
        DisposeIntroMovie();
        DisposeSoundtrack();
        StopEffectVoice();
        foreach (var sound in _combatSounds.Values) sound.Dispose();
        foreach (var sound in _generalSounds.Values) sound.Dispose();
        _combatSounds.Clear();
        _generalSounds.Clear();
        // Closing the window while an online match is running should tell the server so, and let go
        // of the sockets either way.
        ReleaseOnlineResources();
        base.UnloadContent();
    }
}

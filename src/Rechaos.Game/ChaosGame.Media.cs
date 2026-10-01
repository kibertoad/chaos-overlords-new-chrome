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
    private bool _soundtrackPausedByDeactivation;
    private TimeSpan _soundtrackStartDeadline;
    private readonly SoundtrackRestartPoll _soundtrackRestartPoll = new();
    private SoundtrackFade? _soundtrackFade;
    private OriginalSoundtrackMode? _soundtrackModeAfterFade;

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
            ApplyMusicVolumeLevel(_musicVolumeLevel, TimeSpan.Zero);
        }
        catch
        {
            DisposeSoundtrack();
        }
    }

    private void UpdateSoundtrack(GameTime gameTime)
    {
        var poll = _soundtrackRestartPoll.Advance(_soundtrackEnabled, IsActive, gameTime.TotalGameTime);
        try
        {
            if (AdvanceSoundtrackFade(gameTime.TotalGameTime)) return;
            if (!_soundtrackEnabled || _introMoviesPlaying || !IsActive) return;
            SelectSoundtrackMode(SoundtrackContext(), gameTime.TotalGameTime, _restartSoundtrackProgram);
            _restartSoundtrackProgram = false;
            if (_soundtrackFade is not null || _activeSoundtrack.Count == 0) return;
            if (MediaPlayer.State == MediaState.Playing)
            {
                _soundtrackAwaitingStart = false;
                return;
            }
            if (MediaPlayer.State == MediaState.Paused) return;

            if (_soundtrackAwaitingStart)
            {
                if (gameTime.TotalGameTime < _soundtrackStartDeadline) return;
                DisableSoundtrack();
                return;
            }

            if (poll) StartSoundtrackProgram(gameTime.TotalGameTime);
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void StartSoundtrackProgram(TimeSpan now)
    {
        if (!_soundtrackEnabled || _activeSoundtrack.Count == 0 || !IsActive) return;
        try
        {
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
            // RULE-AUDIO-001, FND-AUDIO-007: a stopped program restarts at its first track.
            MediaPlayer.Play(_activeSoundtrack, index: 0);
            _restartSoundtrackProgram = false;
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
        if (!_soundtrackEnabled) return;
        _soundtrackMode = null;
        _soundtrackFade = null;
        _soundtrackModeAfterFade = null;
        _soundtrackAwaitingStart = false;
        _soundtrackPausedByDeactivation = false;
        try
        {
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
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

        if (MediaPlayer.State == MediaState.Playing)
        {
            _soundtrackModeAfterFade = mode;
            BeginSoundtrackFade(now);
            return;
        }
        SetSoundtrackMode(mode, now);
    }

    private void SetSoundtrackMode(OriginalSoundtrackMode mode, TimeSpan now)
    {
        if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
        _soundtrackMode = mode;
        _soundtrackAwaitingStart = false;
        _soundtrackPausedByDeactivation = false;
        _activeSoundtrack = SongCollection.Empty.Clone();
        // RULE-AUDIO-001: let the player advance within the complete CD program.
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
        MediaPlayer.Volume = _soundtrackFade.VolumeAt(now);
        _soundtrackAwaitingStart = false;
    }

    private bool AdvanceSoundtrackFade(TimeSpan now)
    {
        if (_soundtrackFade is not { } fade) return false;
        MediaPlayer.Volume = fade.VolumeAt(now);
        if (!fade.IsComplete(now)) return true;
        MediaPlayer.Stop();
        MediaPlayer.Volume = fade.RestoredVolume;
        _soundtrackFade = null;
        if (_soundtrackModeAfterFade is { } mode)
        {
            _soundtrackModeAfterFade = null;
            SetSoundtrackMode(mode, now);
        }
        return true;
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
        if (_soundtrackEnabled)
        {
            try
            {
                if (MediaPlayer.State == MediaState.Playing)
                {
                    MediaPlayer.Pause();
                    _soundtrackPausedByDeactivation = true;
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
        if (!_soundtrackEnabled) return;
        try
        {
            // RULE-AUDIO-002, FND-AUDIO-007: activation reapplies levels before resuming.
            MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(_musicVolumeLevel);
            if (_activeEffectVoice is not null)
                _activeEffectVoice.Volume = AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel);
            if (_soundtrackPausedByDeactivation && MediaPlayer.State == MediaState.Paused)
                MediaPlayer.Resume();
            _soundtrackPausedByDeactivation = false;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void ApplyMusicVolumeLevel(int level, TimeSpan now)
    {
        if (level is < OriginalSoundtrackPolicy.MinimumVolumeLevel
            or > OriginalSoundtrackPolicy.MaximumVolumeLevel)
            throw new ArgumentOutOfRangeException(nameof(level));

        _musicVolumeLevel = level;
        if (_soundtrack.Count == 0 || _soundtrackFailed) return;
        try
        {
            if (level == 0)
            {
                _soundtrackEnabled = false;
                _soundtrackAwaitingStart = false;
                _soundtrackPausedByDeactivation = false;
                if (MediaPlayer.State == MediaState.Playing) BeginSoundtrackFade(now);
                else if (_soundtrackFade is null && MediaPlayer.State != MediaState.Stopped)
                    MediaPlayer.Stop();
                return;
            }

            MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(level);
            var shouldStart = !_soundtrackEnabled;
            _soundtrackEnabled = true;
            // RULE-AUDIO-003: enabling music leaves playback to the next poll.
            if (_soundtrackFade is not null || !shouldStart || _introMoviesPlaying) return;
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
        _soundtrackModeAfterFade = null;
        _soundtrackEnabled = false;
        _soundtrackAwaitingStart = false;
        _soundtrackPausedByDeactivation = false;
        try
        {
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
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

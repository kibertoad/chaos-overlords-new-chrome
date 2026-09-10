using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly TimeSpan SoundtrackStartTimeout = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, Song> _soundtrack =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<Song> _activeSoundtrack = [];
    private OriginalSoundtrackMode? _soundtrackMode;
    private int _soundtrackIndex = -1;
    private int _musicVolumeLevel = OriginalSoundtrackPolicy.DefaultVolumeLevel;
    private bool _soundtrackEnabled;
    private bool _soundtrackFailed;
    private bool _soundtrackAwaitingStart;
    private bool _soundtrackPausedByDeactivation;
    private TimeSpan _soundtrackStartDeadline;

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
        if (!_soundtrackEnabled) return;
        try
        {
            SelectSoundtrackMode(SoundtrackContext(), gameTime.TotalGameTime);
            if (_activeSoundtrack.Count == 0) return;
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

            StartNextSoundtrackTrack(gameTime.TotalGameTime);
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void StartNextSoundtrackTrack(TimeSpan now)
    {
        if (!_soundtrackEnabled || _activeSoundtrack.Count == 0) return;
        try
        {
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
            _soundtrackIndex = (_soundtrackIndex + 1) % _activeSoundtrack.Count;
            MediaPlayer.Play(_activeSoundtrack[_soundtrackIndex]);
            _soundtrackAwaitingStart = true;
            _soundtrackStartDeadline = now + SoundtrackStartTimeout;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void SelectSoundtrackMode(ClientScreen screen, TimeSpan now)
    {
        var mode = OriginalSoundtrackPolicy.ModeFor(screen);
        if (_soundtrackMode == mode) return;

        if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
        _soundtrackMode = mode;
        _soundtrackIndex = -1;
        _soundtrackAwaitingStart = false;
        _soundtrackPausedByDeactivation = false;
        _activeSoundtrack = OriginalSoundtrackPolicy.FileNamesFor(mode)
            .Select(fileName => _soundtrack.GetValueOrDefault(fileName))
            .Where(song => song is not null)
            .Cast<Song>()
            .ToArray();
        StartNextSoundtrackTrack(now);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
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
        if (!_soundtrackEnabled || !_soundtrackPausedByDeactivation) return;
        try
        {
            if (MediaPlayer.State == MediaState.Paused) MediaPlayer.Resume();
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
            MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(level);
            if (level == 0)
            {
                _soundtrackEnabled = false;
                _soundtrackAwaitingStart = false;
                _soundtrackPausedByDeactivation = false;
                if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
                return;
            }

            var shouldStart = !_soundtrackEnabled;
            _soundtrackEnabled = true;
            if (!shouldStart) return;
            _soundtrackMode = null;
            SelectSoundtrackMode(SoundtrackContext(), now);
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void DisableSoundtrack()
    {
        _soundtrackFailed = true;
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
        _activeSoundtrack = [];
        _soundtrackMode = null;
        _soundtrackIndex = -1;
    }

    protected override void UnloadContent()
    {
        DisposeSoundtrack();
        foreach (var sound in _weaponSounds.Values) sound.Dispose();
        foreach (var sound in _generalSounds.Values) sound.Dispose();
        _weaponSounds.Clear();
        _generalSounds.Clear();
        base.UnloadContent();
    }
}

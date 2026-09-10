using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly TimeSpan SoundtrackStartTimeout = TimeSpan.FromSeconds(2);

    private readonly List<Song> _soundtrack = [];
    private int _soundtrackIndex = -1;
    private bool _soundtrackEnabled;
    private bool _soundtrackAwaitingStart;
    private TimeSpan _soundtrackStartDeadline;

    private void LoadSoundtrack()
    {
        foreach (var path in SoundtrackCatalog.FindAvailableTracks(_assetRoot))
        {
            try
            {
                _soundtrack.Add(Song.FromUri(
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
            _soundtrackEnabled = true;
            MediaPlayer.IsRepeating = false;
            MediaPlayer.IsShuffled = false;
            StartNextSoundtrackTrack(TimeSpan.Zero);
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
        if (!_soundtrackEnabled || _soundtrack.Count == 0) return;
        try
        {
            // Until the original CD selection policy is recovered, preserve the
            // physical Track02..Track09 order and wrap after the last track.
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
            _soundtrackIndex = (_soundtrackIndex + 1) % _soundtrack.Count;
            MediaPlayer.Play(_soundtrack[_soundtrackIndex]);
            _soundtrackAwaitingStart = true;
            _soundtrackStartDeadline = now + SoundtrackStartTimeout;
        }
        catch
        {
            DisableSoundtrack();
        }
    }

    private void DisableSoundtrack()
    {
        _soundtrackEnabled = false;
        _soundtrackAwaitingStart = false;
        try
        {
            if (MediaPlayer.State != MediaState.Stopped) MediaPlayer.Stop();
        }
        catch
        {
            // Music is optional presentation; backend failure must not stop play.
        }
    }

    private void DisposeSoundtrack()
    {
        DisableSoundtrack();
        foreach (var song in _soundtrack) song.Dispose();
        _soundtrack.Clear();
        _soundtrackIndex = -1;
    }

    protected override void UnloadContent()
    {
        DisposeSoundtrack();
        base.UnloadContent();
    }
}

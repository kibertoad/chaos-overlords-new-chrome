using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private const string IntroMoviesUnavailable = "INTRO VIDEO UNAVAILABLE";

    private readonly Queue<string> _introMoviePaths = [];
    private SmackerMovieStream? _introMovie;
    private SmackerPlaybackTimeline? _introMovieTimeline;
    private string? _introMovieFileName;
    private DynamicSoundEffectInstance? _introMovieAudio;
    private Texture2D? _introMovieTexture;
    private bool _introMoviesPlaying;
    private bool _introMovieReached;
    private bool _introMoviesSeen;

    /// <summary>Streams the movies unattended on the first run only; every later run reaches
    /// them through the title screen instead.</summary>
    private void InitializeIntroMovies()
    {
        if (IntroMoviePolicy.PlaysAtStartup(_introMoviesSeen)) BeginIntroMovies();
    }

    private void ReplayIntroMovies()
    {
        if (BeginIntroMovies()) _message = string.Empty;
        else RejectInput(IntroMoviesUnavailable);
    }

    /// <summary>Queues every readable movie and takes the update loop over until it drains.</summary>
    private bool BeginIntroMovies()
    {
        if (_introMoviesPlaying) return true;
        foreach (var fileName in IntroMoviePolicy.FileNames)
        {
            var path = Path.Combine(_assetRoot, "video", fileName);
            if (File.Exists(path)) _introMoviePaths.Enqueue(path);
            else WriteIntroMovieDiagnostic("movie.missing", fileName, null);
        }
        if (_introMoviePaths.Count == 0) return false;
        _introMoviesPlaying = true;
        _introMovieReached = false;
        SuspendSoundtrackForIntroMovies();
        return true;
    }

    private bool UpdateIntroMovies(
        GameTime gameTime,
        KeyboardState keyboard,
        MouseState mouse)
    {
        if (!_introMoviesPlaying) return false;
        if (!IsActive) return true;
        var skip = Pressed(keyboard, Keys.Escape)
            || Pressed(keyboard, Keys.Enter)
            || Pressed(keyboard, Keys.Space)
            || PointerButtonEdges.Pressed(mouse.LeftButton, _previousMouse.LeftButton)
            || PointerButtonEdges.Pressed(mouse.RightButton, _previousMouse.RightButton);

        while (_introMoviesPlaying)
        {
            if (_introMovie is null && !TryStartNextIntroMovie()) continue;
            if (skip)
            {
                FinishIntroMovie("movie.skipped", null);
                break;
            }
            try
            {
                var advance = _introMovieTimeline!.Advance(gameTime.ElapsedGameTime);
                for (var index = 0; index < advance.FramesToDecode; index++)
                    PresentIntroMovieFrame(_introMovie!.ReadNextFrame());
                if (!advance.Completed) break;
                FinishIntroMovie("movie.completed", null);
            }
            catch (Exception exception)
            {
                FinishIntroMovie("movie.failed", exception);
            }
        }
        return true;
    }

    private bool TryStartNextIntroMovie()
    {
        if (_introMoviePaths.Count == 0)
        {
            CompleteIntroMovies();
            return false;
        }

        var path = _introMoviePaths.Dequeue();
        try
        {
            _introMovie = SmackerMovieStream.Open(path);
            _introMovieFileName = Path.GetFileName(path);
            _introMovieTimeline = new SmackerPlaybackTimeline(
                _introMovie.Metadata.FrameCount,
                _introMovie.Metadata.FrameDuration);
            WriteIntroMovieDiagnostic("movie.started", _introMovieFileName, null);
            _introMovieReached = true;
            return true;
        }
        catch (Exception exception)
        {
            WriteIntroMovieDiagnostic("movie.failed", Path.GetFileName(path), exception);
            DisposeIntroMovie();
            return false;
        }
    }

    private void PresentIntroMovieFrame(SmackerMovieFrame frame)
    {
        if (_introMovieTexture is null
            || _introMovieTexture.Width != frame.Video.Width
            || _introMovieTexture.Height != frame.Video.Height)
        {
            _introMovieTexture?.Dispose();
            _introMovieTexture = new Texture2D(
                GraphicsDevice, frame.Video.Width, frame.Video.Height);
        }

        var palette = frame.Palette;
        var indices = frame.Video.ColorIndices.Span;
        var colors = new Color[indices.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            var color = palette[indices[index]];
            colors[index] = new Color(color.Red, color.Green, color.Blue, byte.MaxValue);
        }
        _introMovieTexture.SetData(colors);

        foreach (var audio in frame.Audio)
        {
            if (audio.UnsignedPcm8.IsEmpty) continue;
            EnsureIntroMovieAudio(audio);
            _introMovieAudio!.SubmitBuffer(
                SmackerPcmConversion.ToSigned16LittleEndian(audio.UnsignedPcm8.Span));
            if (_introMovieAudio.State != SoundState.Playing) _introMovieAudio.Play();
        }
    }

    private void EnsureIntroMovieAudio(SmackerMovieAudio audio)
    {
        if (_introMovieAudio is not null) return;
        _introMovieAudio = new DynamicSoundEffectInstance(
            audio.SampleRate,
            audio.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        _introMovieAudio.Volume = AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel);
    }

    private void DrawIntroMovie(SpriteBatch batch)
    {
        if (_introMovieTexture is null) return;
        batch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: VirtualInput.Transform(GraphicsDevice.Viewport));
        batch.Draw(
            _introMovieTexture,
            IntroMoviePolicy.Destination(
                _introMovieTexture.Width, _introMovieTexture.Height),
            Color.White);
        batch.End();
    }

    private void FinishIntroMovie(string eventName, Exception? exception)
    {
        WriteIntroMovieDiagnostic(eventName, _introMovieFileName, exception);
        DisposeIntroMovie();
        if (_introMoviePaths.Count == 0) CompleteIntroMovies();
    }

    /// <summary>Hands the loop back and records that the intro no longer owes the player a
    /// showing, so the next run starts at the title. A pack whose files all failed to open
    /// showed nothing, and leaves that debt standing.</summary>
    private void CompleteIntroMovies()
    {
        _introMoviesPlaying = false;
        if (_introMoviesSeen || !_introMovieReached) return;
        _introMoviesSeen = true;
        SavePreferences();
    }

    private void WriteIntroMovieDiagnostic(string eventName, string? fileName, Exception? exception)
    {
        _diagnostics?.Write(eventName, new Dictionary<string, string?>
        {
            ["file"] = fileName,
            ["error"] = RuntimeDiagnostics.ExceptionType(exception)
        });
    }

    private void DisposeIntroMovie()
    {
        try
        {
            _introMovieAudio?.Stop();
            _introMovieAudio?.Dispose();
        }
        catch
        {
            // A failing optional audio backend must not prevent movie cleanup.
        }
        _introMovieAudio = null;
        _introMovie?.Dispose();
        _introMovie = null;
        _introMovieFileName = null;
        _introMovieTimeline = null;
        _introMovieTexture?.Dispose();
        _introMovieTexture = null;
    }
}

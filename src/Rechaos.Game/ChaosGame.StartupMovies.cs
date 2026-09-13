using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private readonly Queue<string> _startupMoviePaths = [];
    private SmackerMovieStream? _startupMovie;
    private SmackerPlaybackTimeline? _startupMovieTimeline;
    private string? _startupMovieFileName;
    private DynamicSoundEffectInstance? _startupMovieAudio;
    private Texture2D? _startupMovieTexture;
    private bool _startupMoviesComplete = true;

    private void InitializeStartupMovies()
    {
        foreach (var fileName in StartupMoviePolicy.FileNames)
        {
            var path = Path.Combine(_assetRoot, "video", fileName);
            if (File.Exists(path)) _startupMoviePaths.Enqueue(path);
            else WriteStartupMovieDiagnostic("movie.missing", fileName, null);
        }
        _startupMoviesComplete = _startupMoviePaths.Count == 0;
    }

    private bool UpdateStartupMovies(
        GameTime gameTime,
        KeyboardState keyboard,
        MouseState mouse)
    {
        if (_startupMoviesComplete) return false;
        if (!IsActive) return true;
        var skip = Pressed(keyboard, Keys.Escape)
            || Pressed(keyboard, Keys.Enter)
            || Pressed(keyboard, Keys.Space)
            || PointerButtonEdges.Pressed(mouse.LeftButton, _previousMouse.LeftButton)
            || PointerButtonEdges.Pressed(mouse.RightButton, _previousMouse.RightButton);

        while (!_startupMoviesComplete)
        {
            if (_startupMovie is null && !TryStartNextStartupMovie()) continue;
            if (skip)
            {
                FinishStartupMovie("movie.skipped", null);
                break;
            }
            try
            {
                var advance = _startupMovieTimeline!.Advance(gameTime.ElapsedGameTime);
                for (var index = 0; index < advance.FramesToDecode; index++)
                    PresentStartupMovieFrame(_startupMovie!.ReadNextFrame());
                if (!advance.Completed) break;
                FinishStartupMovie("movie.completed", null);
            }
            catch (Exception exception)
            {
                FinishStartupMovie("movie.failed", exception);
            }
        }
        return true;
    }

    private bool TryStartNextStartupMovie()
    {
        if (_startupMoviePaths.Count == 0)
        {
            _startupMoviesComplete = true;
            return false;
        }

        var path = _startupMoviePaths.Dequeue();
        try
        {
            _startupMovie = SmackerMovieStream.Open(path);
            _startupMovieFileName = Path.GetFileName(path);
            _startupMovieTimeline = new SmackerPlaybackTimeline(
                _startupMovie.Metadata.FrameCount,
                _startupMovie.Metadata.FrameDuration);
            WriteStartupMovieDiagnostic("movie.started", _startupMovieFileName, null);
            return true;
        }
        catch (Exception exception)
        {
            WriteStartupMovieDiagnostic("movie.failed", Path.GetFileName(path), exception);
            DisposeStartupMovie();
            return false;
        }
    }

    private void PresentStartupMovieFrame(SmackerMovieFrame frame)
    {
        if (_startupMovieTexture is null
            || _startupMovieTexture.Width != frame.Video.Width
            || _startupMovieTexture.Height != frame.Video.Height)
        {
            _startupMovieTexture?.Dispose();
            _startupMovieTexture = new Texture2D(
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
        _startupMovieTexture.SetData(colors);

        foreach (var audio in frame.Audio)
        {
            if (audio.UnsignedPcm8.IsEmpty) continue;
            EnsureStartupMovieAudio(audio);
            _startupMovieAudio!.SubmitBuffer(
                SmackerPcmConversion.ToSigned16LittleEndian(audio.UnsignedPcm8.Span));
            if (_startupMovieAudio.State != SoundState.Playing) _startupMovieAudio.Play();
        }
    }

    private void EnsureStartupMovieAudio(SmackerMovieAudio audio)
    {
        if (_startupMovieAudio is not null) return;
        _startupMovieAudio = new DynamicSoundEffectInstance(
            audio.SampleRate,
            audio.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        _startupMovieAudio.Volume = AudioRouting.EffectVolumeForLevel(_soundEffectVolumeLevel);
    }

    private void DrawStartupMovie(SpriteBatch batch)
    {
        if (_startupMovieTexture is null) return;
        batch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: VirtualInput.Transform(GraphicsDevice.Viewport));
        batch.Draw(
            _startupMovieTexture,
            StartupMoviePolicy.Destination(
                _startupMovieTexture.Width, _startupMovieTexture.Height),
            Color.White);
        batch.End();
    }

    private void FinishStartupMovie(string eventName, Exception? exception)
    {
        WriteStartupMovieDiagnostic(eventName, _startupMovieFileName, exception);
        DisposeStartupMovie();
        if (_startupMoviePaths.Count == 0) _startupMoviesComplete = true;
    }

    private void WriteStartupMovieDiagnostic(string eventName, string? fileName, Exception? exception)
    {
        _diagnostics?.Write(eventName, new Dictionary<string, string?>
        {
            ["file"] = fileName,
            ["error"] = exception?.Message
        });
    }

    private void DisposeStartupMovie()
    {
        try
        {
            _startupMovieAudio?.Stop();
            _startupMovieAudio?.Dispose();
        }
        catch
        {
            // A failing optional audio backend must not prevent movie cleanup.
        }
        _startupMovieAudio = null;
        _startupMovie?.Dispose();
        _startupMovie = null;
        _startupMovieFileName = null;
        _startupMovieTimeline = null;
        _startupMovieTexture?.Dispose();
        _startupMovieTexture = null;
    }
}

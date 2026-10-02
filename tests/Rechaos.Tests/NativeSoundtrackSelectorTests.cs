using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

[Collection("Native soundtrack")]
public sealed class NativeSoundtrackSelectorTests
{
    private static readonly MethodInfo UpdateSoundtrackMethod = Method("UpdateSoundtrack");
    private static readonly MethodInfo ApplyAudioVolumeLevelsMethod = Method("ApplyAudioVolumeLevels");

    [Fact]
    public void SelectorKeepsTheOldProgramUntilTheFadeEndsAndThenStartsTheNewOne()
    {
        // RULE-AUDIO-001, RULE-AUDIO-003, FND-AUDIO-016: the selector holds
        // its old mode during the zero-based fade, then stops and selects anew.
        RunWithTitlePlaying(ClientScreen.City, (game, title, gameplay, stoppedVolume) =>
        {
            Tick(game, 0);
            AssertFadeHoldsTitle(game, title);
            Tick(game, 526);
            Assert.Equal(OriginalSoundtrackMode.Title, Mode(game));
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.Same(title, MediaPlayer.Queue.ActiveSong);
            Assert.Null(stoppedVolume());
            Tick(game, 527);
            Assert.Equal(0f, stoppedVolume());
            Assert.Null(Field("_soundtrackFade").GetValue(game));
            Assert.Equal(OriginalSoundtrackMode.Gameplay, Mode(game));
            Assert.Same(gameplay, MediaPlayer.Queue.ActiveSong);
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.Equal(OriginalSoundtrackPolicy.VolumeForLevel(5), MediaPlayer.Volume);
            Assert.False((bool)Field("_soundtrackFailed").GetValue(game)!);
        });
    }

    [Fact]
    public void MutedFadeStopsAndReenabledMusicWaitsForTheNextPresentationPoll()
    {
        // RULE-AUDIO-002, RULE-AUDIO-003, FND-AUDIO-016: the mute fade stops at
        // zero, applying a nonzero level does not play, and only the next
        // presentation poll restarts the program.
        RunWithTitlePlaying(ClientScreen.Title, (game, title, _, stoppedVolume) =>
        {
            ApplyMusicLevel(game, 0, TimeSpan.Zero);
            Tick(game, 0);
            AssertFadeHoldsTitle(game, title);
            Tick(game, 526);
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.Null(stoppedVolume());
            Tick(game, 527);
            Assert.Equal(0f, stoppedVolume());
            Assert.Null(Field("_soundtrackFade").GetValue(game));
            Assert.Equal(MediaState.Stopped, MediaPlayer.State);
            Assert.Equal(OriginalSoundtrackMode.Title, Mode(game));
            // Consume the pending presentation tick while still muted.
            Tick(game, 528);
            ApplyMusicLevel(game, 5, TimeSpan.FromMilliseconds(529));
            Assert.Equal(MediaState.Stopped, MediaPlayer.State);
            Tick(game, 530);
            Assert.Equal(MediaState.Stopped, MediaPlayer.State);
            Tick(game, 4 * PresentationClock.PeriodMilliseconds);
            Assert.Equal(MediaState.Playing, MediaPlayer.State);
            Assert.Same(title, MediaPlayer.Queue.ActiveSong);
            Assert.False((bool)Field("_soundtrackFailed").GetValue(game)!);
        });
    }

    private delegate void TitlePlayingScenario(ChaosGame game, Song title, Song gameplay, Func<float?> stoppedVolume);

    private static void RunWithTitlePlaying(ClientScreen screen, TitlePlayingScenario scenario)
    {
        var uri = new Uri(Path.Combine(AppContext.BaseDirectory, "Fixtures", "silence.ogg"));
        using var title = Song.FromUri("selector-title", uri);
        using var gameplay = Song.FromUri("selector-gameplay", uri);
        using var player = new SoundtrackProgramPlayer([title, gameplay]);
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        var screens = new ScreenRouter();
        screens.Show(screen);
        Set(game, "_screens", screens);
        Set(game, "_soundtrack", new Dictionary<string, Song>
        {
            [OriginalSoundtrackPolicy.FileNamesFor(OriginalSoundtrackMode.Title)[0]] = title,
            [OriginalSoundtrackPolicy.FileNamesFor(OriginalSoundtrackMode.Gameplay)[0]] = gameplay
        });
        Set(game, "_soundtrackProgramPlayer", player);
        Set(game, "_soundtrackMode", OriginalSoundtrackMode.Title);
        Set(game, "_soundtrackEnabled", true);
        Set(game, "_musicVolumeLevel", 5);
        Set(game, "_soundtrackFocus", new SoundtrackFocusState());
        Set(game, "_soundtrackRestartPoll", new SoundtrackRestartPoll());
        var active = SongCollection.Empty.Clone();
        active.Add(title);
        Set(game, "_activeSoundtrack", active);
        float? stoppedVolume = null;
        EventHandler<EventArgs> observeStop = (_, _) =>
        {
            if (MediaPlayer.State == MediaState.Stopped) stoppedVolume = MediaPlayer.Volume;
        };
        MediaPlayer.MediaStateChanged += observeStop;
        try
        {
            MediaPlayer.IsRepeating = false;
            MediaPlayer.IsShuffled = false;
            MediaPlayer.Volume = OriginalSoundtrackPolicy.VolumeForLevel(5);
            // Hold both native preparation locks so natural completion cannot
            // turn this simulated game-clock comparison into a wall-clock race.
            lock (PreparationLock(title))
            lock (PreparationLock(gameplay))
            {
                player.PlayProgram([title]);
                scenario(game, title, gameplay, () => stoppedVolume);
            }
        }
        finally
        {
            MediaPlayer.MediaStateChanged -= observeStop;
        }
    }

    private static void AssertFadeHoldsTitle(ChaosGame game, Song title)
    {
        Assert.Equal(OriginalSoundtrackMode.Title, Mode(game));
        Assert.Same(title, MediaPlayer.Queue.ActiveSong);
        Assert.NotNull(Field("_soundtrackFade").GetValue(game));
        Assert.Equal(new SoundtrackFade(OriginalSoundtrackPolicy.VolumeForLevel(5), TimeSpan.Zero)
            .VolumeAt(TimeSpan.Zero), MediaPlayer.Volume);
    }

    private static object PreparationLock(Song song)
    {
        var stream = typeof(Song).GetField("stream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(song)!;
        return stream.GetType().GetField("prepareMutex", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(stream)!;
    }

    private static void ApplyMusicLevel(ChaosGame game, int level, TimeSpan now)
    {
        Set(game, "_musicVolumeLevel", level);
        ApplyAudioVolumeLevelsMethod.Invoke(game, [now]);
    }

    private static void Tick(ChaosGame game, int milliseconds) => UpdateSoundtrackMethod
        .Invoke(game, [new GameTime(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.Zero)]);
    private static object? Mode(ChaosGame game) => Field("_soundtrackMode").GetValue(game);
    private static void Set(ChaosGame game, string name, object value) => Field(name).SetValue(game, value);
    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
    private static MethodInfo Method(string name) => typeof(ChaosGame).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(name);
}

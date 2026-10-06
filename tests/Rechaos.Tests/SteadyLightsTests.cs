using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// DEV-UI-027: with Steady Lights on, the console lights stay lit and the selected sector's frame
/// holds its first frame at every tick; off, they blink and cycle on the presentation clock as
/// SCR-UI-003 records. The setting starts off and survives a preferences round trip.
/// </summary>
public sealed class SteadyLightsTests
{
    private static readonly TimeSpan[] Times =
        Enumerable.Range(0, 16).Select(tick => PresentationClock.Period * tick).ToArray();

    [Fact]
    public void OffTheLightsBlinkAndTheSelectionFrameCycles()
    {
        var game = GameAt(steady: false);

        var lit = Times.Select(time => Lit(game, time)).ToArray();
        var frames = Times.Select(time => SelectionFrame(game, time)).ToArray();

        Assert.Contains(true, lit);
        Assert.Contains(false, lit);
        Assert.Contains(0, frames);
        Assert.Contains(1, frames);
    }

    [Fact]
    public void OnTheLightsStayLitAndTheSelectionFrameHolds()
    {
        var game = GameAt(steady: true);

        Assert.All(Times, time => Assert.True(Lit(game, time)));
        Assert.All(Times, time => Assert.Equal(0, SelectionFrame(game, time)));
    }

    [Fact]
    public void ARecordedLampPhaseStillWins()
    {
        // The reference frame passes the phase a capture recorded, which the setting never
        // overrides; captures are compared with every setting off.
        var game = GameAt(steady: true);

        Assert.False((bool)DeviationBehaviourTests.Call(game, "LampInLitPhase", (bool?)false)!);
    }

    [Fact]
    public void TheSettingStartsOffAndRoundTrips()
    {
        Assert.False(OriginalOptionsPolicy.SteadyLightsByDefault);
        Assert.False(GamePreferences.Default.SteadyLights);

        var directory = Directory.CreateTempSubdirectory("rechaos-steady-lights-");
        try
        {
            var path = Path.Combine(directory.FullName, "preferences.json");
            var expected = GamePreferences.Default with { SteadyLights = true };
            Assert.True(GamePreferencesStore.TrySave(path, expected));
            Assert.Equal(expected, GamePreferencesStore.LoadOrDefault(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void VersionTwelvePreferencesKeepTheirChoicesAndStartTheSettingOff()
    {
        var directory = Directory.CreateTempSubdirectory("rechaos-steady-lights-");
        try
        {
            var path = Path.Combine(directory.FullName, "preferences.json");
            File.WriteAllText(path, """
                {"FormatVersion":12,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
                 "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
                 "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":true,
                 "Fullscreen":true,"SmoothEventSiteImages":true,"IntroMoviesSeen":true,
                 "DefaultAiPolicy":1,"OnlineService":1,
                 "CustomMultiplayerServer":"https://games.example.test","LobbyPresentation":1,
                 "IntroOnlyOnce":false,"PreferredScenario":3}
                """);

            var preferences = GamePreferencesStore.LoadOrDefault(path);

            Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
            Assert.False(preferences.IntroOnlyOnce);
            Assert.Equal((ScenarioId)3, preferences.PreferredScenario);
            Assert.Equal(OnlineLobbyPresentation.Classic, preferences.LobbyPresentation);
            Assert.True(preferences.SlidePanels);
            Assert.False(preferences.SteadyLights);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static ChaosGame GameAt(bool steady)
    {
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_steadyLights").SetValue(game, steady);
        return game;
    }

    private static void Advance(ChaosGame game, TimeSpan time) =>
        ((EventPumpClock)DeviationBehaviourTests.Field("_eventPump").GetValue(game)!).Update(time, holding: false);

    private static bool Lit(ChaosGame game, TimeSpan time)
    {
        Advance(game, time);
        return (bool)DeviationBehaviourTests.Call(game, "LampInLitPhase", (bool?)null)!;
    }

    private static int SelectionFrame(ChaosGame game, TimeSpan time)
    {
        Advance(game, time);
        return (int)DeviationBehaviourTests.Call(game, "SelectionFrameShown")!;
    }
}

using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The options a fresh start has, against the initialized values of RULE-OPTIONS-001. Slide
/// Panels and Full screen start as DEV-OPTIONS-002 and DEV-OPTIONS-003 set them; both are
/// settings, and switched off they give the original's 1.
/// </summary>
public sealed class OriginalOptionsDefaultsTests
{
    [Fact]
    public void FreshOptionsTakeTheInitializedValuesOfRuleOptions001()
    {
        var preferences = GamePreferences.Default;

        // prefsFreeGang 1, prefsBaseStats 0, prefsCombat 1.
        Assert.True(preferences.WarnIfIdleGangs);
        Assert.False(preferences.ShowBaseStatistics);
        Assert.True(preferences.DetailedCombat);
        // prefsVolumeSFX 6, prefsVolumeCD 5.
        Assert.Equal(6, preferences.SoundEffectVolumeLevel);
        Assert.Equal(5, preferences.MusicVolumeLevel);
        // prefsTimeLimit 0, prefsObjective 0 (Greed), prefsDiff 1 (Criminal).
        Assert.Equal(PlanningTimeLimit.None, preferences.PlanningTimeLimit);
        Assert.Equal(ScenarioId.Greed, preferences.PreferredScenario);
        Assert.Equal(AiDifficulty.Criminal, OriginalOptionsPolicy.MentalityByDefault);
        Assert.Equal(1, (int)OriginalOptionsPolicy.MentalityByDefault);
    }

    [Fact]
    public void SlidePanelsAndFullScreenStartAsTheirDeviationsSetThem()
    {
        // DEV-OPTIONS-002 and DEV-OPTIONS-003 are on by default and start both options off.
        Assert.False(GamePreferences.Default.SlidePanels);
        Assert.False(GamePreferences.Default.Fullscreen);
    }
}

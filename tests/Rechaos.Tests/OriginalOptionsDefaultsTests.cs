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

    // FND-OPTIONS-001: the globals the options are read into, with the values the executable
    // initializes them to.
    [Fact]
    public void FreshOptionsTakeTheValuesTheExecutableInitializes()
    {
        // needs: GAME_DIR
        var file = ExecutableResources.RequireExecutable();
        // RULE-OPTIONS-001: every option compared here keeps only the low byte of its global.
        int At(uint address) => ExecutableResources.ImageInt32(file, address) & 0xFF;
        var preferences = GamePreferences.Default;

        Assert.Equal(At(0x00487860) != 0, preferences.WarnIfIdleGangs);
        Assert.Equal(At(0x0048784C) != 0, preferences.ShowBaseStatistics);
        Assert.Equal(At(0x0048785C) != 0, preferences.DetailedCombat);
        Assert.Equal(At(0x00487864), preferences.SoundEffectVolumeLevel);
        Assert.Equal(At(0x00487868), preferences.MusicVolumeLevel);
        Assert.Equal(At(0x00487854), (int)preferences.PlanningTimeLimit);
        Assert.Equal(At(0x00487858), ExecutableStrings.ScenarioNumber(preferences.PreferredScenario));
        Assert.Equal((int)OriginalOptionsPolicy.MentalityByDefault, At(0x00487850));
        // Slide Panels and Full screen start as DEV-OPTIONS-002 and DEV-OPTIONS-003 set them. The
        // rebuild has no Thousands of Colors option (DEV-UI-016) and keeps no communication type,
        // and no serial number (DEV-RNG-001).
    }

    [Fact]
    public void SlidePanelsAndFullScreenStartAsTheirDeviationsSetThem()
    {
        // DEV-OPTIONS-002 and DEV-OPTIONS-003 are on by default and start both options off.
        Assert.False(GamePreferences.Default.SlidePanels);
        Assert.False(GamePreferences.Default.Fullscreen);
    }
}

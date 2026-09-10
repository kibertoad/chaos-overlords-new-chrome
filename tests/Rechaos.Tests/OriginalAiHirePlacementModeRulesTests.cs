using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiHirePlacementModeRulesTests
{
    public static TheoryData<ScenarioId> HostileOverrideScenarios => new()
    {
        ScenarioId.Greed,
        ScenarioId.Power,
        ScenarioId.Acceptance,
        ScenarioId.Dominance,
        ScenarioId.KillEmAll,
        ScenarioId.Big40,
        ScenarioId.Armageddon
    };

    [Theory]
    [MemberData(nameof(HostileOverrideScenarios))]
    public void RoleFourUsesFirstVisibleHostileSectorForSevenScenarios(
        ScenarioId scenario)
    {
        Assert.Equal(91, Select(scenario, role: 4, hostileSector: 27));
    }

    [Theory]
    [MemberData(nameof(HostileOverrideScenarios))]
    public void RoleFourEncodesTheRawNoHostileSentinel(
        ScenarioId scenario)
    {
        Assert.Equal(AiPlanningState.InactiveSectorAnchor, Select(
            scenario,
            role: 4,
            hostileSector: OriginalAiHirePlacementRules.InactiveGangSector));
    }

    [Theory]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.BigMan)]
    public void RoleFourRetainsAnchorForEliminateAndBigMan(ScenarioId scenario)
    {
        Assert.Equal(82, Select(scenario, role: 4, hostileSector: 27, gangZero: 12));
    }

    [Fact]
    public void SiegeRoleFourUsesEncodedGangSlotZeroSector()
    {
        Assert.Equal(76, Select(
            ScenarioId.Siege, role: 4, hostileSector: 27, gangZero: 12));
        Assert.Equal(AiPlanningState.InactiveSectorAnchor, Select(
            ScenarioId.Siege,
            role: 4,
            hostileSector: OriginalAiHirePlacementRules.InactiveGangSector,
            gangZero: OriginalAiHirePlacementRules.InactiveGangSector));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void EveryNonRoleFourPathRetainsPersistentAnchor(int role)
    {
        foreach (var scenario in Enum.GetValues<ScenarioId>())
            Assert.Equal(82, Select(scenario, role, hostileSector: 27, gangZero: 12));
    }

    [Fact]
    public void AcceptsEveryPersistentEncodedAnchorDomain()
    {
        Assert.Equal(63, Select(ScenarioId.Power, role: 0, anchor: 63));
        Assert.Equal(64, Select(ScenarioId.Power, role: 0, anchor: 64));
        Assert.Equal(127, Select(ScenarioId.Power, role: 0, anchor: 127));
        Assert.Equal(164, Select(ScenarioId.Power, role: 0, anchor: 164));
    }

    [Fact]
    public void RejectsValuesOutsideRecoveredDomains()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select((ScenarioId)10, role: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: 7));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: 4, anchor: 62));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: 4, anchor: 128));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: 4, hostileSector: 64));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Select(ScenarioId.Power, role: 4, gangZero: 64));
    }

    private static int Select(
        ScenarioId scenario,
        int role,
        int anchor = 82,
        int hostileSector = OriginalAiHirePlacementRules.InactiveGangSector,
        int gangZero = 3) =>
        OriginalAiHirePlacementModeRules.Select(
            scenario, role, anchor, hostileSector, gangZero);
}

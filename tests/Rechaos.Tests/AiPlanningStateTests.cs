using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiPlanningStateTests
{
    [Fact]
    public void InitializationUsesOriginalRoleAndFamilyDefaults()
    {
        var planning = AiPlanningState.Initialize();

        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            var playerId = new PlayerId(player);
            Assert.Equal(0, planning.CurrentHireRole(playerId));
            Assert.Equal(0, planning.PreviousHireRole(playerId));
            for (var gang = 0; gang < AiPlanningState.GangSlotsPerPlayer; gang++)
                Assert.Equal(AiPlanningState.UnusedFamily, planning.Family(playerId, gang));
        }
    }

    [Fact]
    public void PlanningStartRollsCurrentRoleIntoPreviousRole()
    {
        var planning = AiPlanningState.Initialize();
        var player = new PlayerId(2);
        planning.SetCurrentHireRole(player, 4);

        planning.BeginPlanning(player);
        planning.SetCurrentHireRole(player, 6);
        planning.SetFamily(player, 80, 14);

        Assert.Equal(4, planning.PreviousHireRole(player));
        Assert.Equal(6, planning.CurrentHireRole(player));
        Assert.Equal(14, planning.Family(player, 80));
    }

    [Fact]
    public void RestoreRejectsInvalidShapesAndValues()
    {
        var roles = new int[MatchLimits.PlayerCount];
        var families = Enumerable.Repeat(
            AiPlanningState.UnusedFamily,
            MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer).ToArray();
        var invalidFamilies = families.ToArray();
        invalidFamilies[0] = 8;
        var invalidRoles = roles.ToArray();
        invalidRoles[0] = 7;

        Assert.Throws<ArgumentException>(() =>
            AiPlanningState.Restore(roles[..^1], roles, families));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(roles, roles, invalidFamilies));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(invalidRoles, roles, families));
    }
}

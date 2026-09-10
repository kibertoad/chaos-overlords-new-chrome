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
            Assert.False(planning.HasPlanned(playerId));
            Assert.Equal(AiPlanningState.InactiveSectorAnchor, planning.SectorAnchor(playerId));
            for (var gang = 0; gang < AiPlanningState.GangSlotsPerPlayer; gang++)
            {
                Assert.Equal(AiPlanningState.UnusedFamily, planning.Family(playerId, gang));
                Assert.Equal(GangAction.None, planning.OlderAction(playerId, gang));
                Assert.Equal(GangAction.None, planning.PreviousAction(playerId, gang));
                Assert.Equal(GangAction.None, planning.PlannedAction(playerId, gang));
                Assert.Equal(AiActionTarget.None, planning.OlderTarget(playerId, gang));
                Assert.Equal(AiActionTarget.None, planning.PreviousTarget(playerId, gang));
                Assert.Equal(AiActionTarget.None, planning.PlannedTarget(playerId, gang));
            }
        }
    }

    [Fact]
    public void PlanningStartRollsThreeActionGenerationsForActiveSlotsOnly()
    {
        var planning = AiPlanningState.Initialize();
        var player = new PlayerId(2);
        MatchGangState[] gangs =
        [
            new(new GangId(20), player, 1, 0, 5),
            new(new GangId(21), player, 1, 0, 0)
        ];
        planning.SetPlannedAction(player, 0, GangAction.Attack, new AiActionTarget(1, 4));
        planning.SetPlannedAction(player, 1, GangAction.Hide, new AiActionTarget(9, 9));
        planning.RollActiveGangActions(player, gangs);
        planning.SetPlannedAction(player, 0, GangAction.Move, new AiActionTarget(62, 0));

        planning.RollActiveGangActions(player, gangs);

        Assert.Equal(GangAction.Attack, planning.OlderAction(player, 0));
        Assert.Equal(GangAction.Move, planning.PreviousAction(player, 0));
        Assert.Equal(GangAction.None, planning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 4), planning.OlderTarget(player, 0));
        Assert.Equal(new AiActionTarget(62, 0), planning.PreviousTarget(player, 0));
        Assert.Equal(AiActionTarget.None, planning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.None, planning.OlderAction(player, 1));
        Assert.Equal(GangAction.None, planning.PreviousAction(player, 1));
        Assert.Equal(GangAction.Hide, planning.PlannedAction(player, 1));
        Assert.Equal(new AiActionTarget(9, 9), planning.PlannedTarget(player, 1));
    }

    [Fact]
    public void PlanningStartRollsCurrentRoleIntoPreviousRole()
    {
        var planning = AiPlanningState.Initialize();
        var player = new PlayerId(2);
        Assert.True(planning.BeginPlanning(player));
        planning.SetCurrentHireRole(player, 4);

        Assert.False(planning.BeginPlanning(player));
        planning.SetCurrentHireRole(player, 6);
        planning.SetFamily(player, 80, 14);
        planning.SetSectorAnchor(player, AiPlanningState.SectorAnchorOffset - 1);

        Assert.Equal(4, planning.PreviousHireRole(player));
        Assert.Equal(6, planning.CurrentHireRole(player));
        Assert.Equal(14, planning.Family(player, 80));
        Assert.Equal(63, planning.SectorAnchor(player));
        Assert.True(planning.HasPlanned(player));
    }

    [Fact]
    public void FirstPlanningPassResetsAllRecordsWithoutRollingActions()
    {
        var roles = new int[MatchLimits.PlayerCount];
        var families = Enumerable.Repeat(
            AiPlanningState.UnusedFamily,
            MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer).ToArray();
        var anchors = Enumerable.Repeat(
            AiPlanningState.InactiveSectorAnchor, MatchLimits.PlayerCount).ToArray();
        var actions = new GangAction[MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer];
        var player = new PlayerId(2);
        var index = player.Value * AiPlanningState.GangSlotsPerPlayer + 3;
        families[index] = 11;
        actions[index] = GangAction.Attack;
        var planning = AiPlanningState.Restore(
            roles, roles, families, anchors, actions, actions, actions,
            new bool[MatchLimits.PlayerCount]);

        Assert.True(planning.BeginPlanning(player));

        Assert.True(planning.HasPlanned(player));
        Assert.Equal(AiPlanningState.UnusedFamily, planning.Family(player, 3));
        Assert.Equal(GangAction.None, planning.OlderAction(player, 3));
        Assert.Equal(GangAction.None, planning.PreviousAction(player, 3));
        Assert.Equal(GangAction.None, planning.PlannedAction(player, 3));
        Assert.Equal(AiActionTarget.None, planning.OlderTarget(player, 3));
        Assert.Equal(AiActionTarget.None, planning.PreviousTarget(player, 3));
        Assert.Equal(AiActionTarget.None, planning.PlannedTarget(player, 3));
    }

    [Fact]
    public void ReusedGangSlotClearsFamilyAndEveryActionGeneration()
    {
        var planning = AiPlanningState.Initialize();
        var player = new PlayerId(2);
        planning.SetFamily(player, 3, 11);
        planning.SetPlannedAction(player, 3, GangAction.Attack);
        planning.RollActiveGangActions(player,
        [
            new(new GangId(20), player, 1, 0, 0),
            new(new GangId(21), player, 1, 0, 0),
            new(new GangId(22), player, 1, 0, 0),
            new(new GangId(23), player, 1, 0, 5)
        ]);
        planning.SetPlannedAction(player, 3, GangAction.Move);

        planning.ResetGangSlot(player, 3);

        Assert.Equal(AiPlanningState.UnusedFamily, planning.Family(player, 3));
        Assert.Equal(GangAction.None, planning.OlderAction(player, 3));
        Assert.Equal(GangAction.None, planning.PreviousAction(player, 3));
        Assert.Equal(GangAction.None, planning.PlannedAction(player, 3));
    }

    [Fact]
    public void CleanupRewritesOnlyFirstDuplicateChaosAndInfluencePerSector()
    {
        var planning = AiPlanningState.Initialize();
        var player = new PlayerId(2);
        MatchGangState[] gangs =
        [
            new(new GangId(20), player, 1, 7, 5),
            new(new GangId(21), player, 1, 7, 5),
            new(new GangId(22), player, 1, 7, 5),
            new(new GangId(23), player, 1, 7, 5),
            new(new GangId(24), player, 1, 7, 5)
        ];
        Assert.True(planning.BeginPlanning(player));
        planning.SetPlannedAction(player, 0, GangAction.Chaos);
        planning.SetPlannedAction(player, 1, GangAction.Chaos);
        planning.SetPlannedAction(player, 2, GangAction.Chaos);
        planning.SetPlannedAction(player, 3, GangAction.Influence);
        planning.SetPlannedAction(player, 4, GangAction.Influence);
        planning.RollActiveGangActions(player, gangs);

        planning.CleanupDuplicatePreviousActions(player, gangs);

        Assert.Equal(GangAction.None, planning.PreviousAction(player, 0));
        Assert.Equal(GangAction.Chaos, planning.PreviousAction(player, 1));
        Assert.Equal(GangAction.Chaos, planning.PreviousAction(player, 2));
        Assert.Equal(GangAction.Snitch, planning.PreviousAction(player, 3));
        Assert.Equal(GangAction.Influence, planning.PreviousAction(player, 4));
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
        var anchors = Enumerable.Repeat(
            AiPlanningState.InactiveSectorAnchor, MatchLimits.PlayerCount).ToArray();
        var invalidAnchors = anchors.ToArray();
        invalidAnchors[0] = 128;
        var actions = new GangAction[MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer];
        var invalidActions = actions.ToArray();
        invalidActions[0] = (GangAction)15;

        Assert.Throws<ArgumentException>(() =>
            AiPlanningState.Restore(roles[..^1], roles, families, anchors));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(roles, roles, invalidFamilies, anchors));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(invalidRoles, roles, families, anchors));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(roles, roles, families, invalidAnchors));
        Assert.Throws<ArgumentException>(() =>
            AiPlanningState.Restore(roles, roles, families, anchors,
                actions[..^1], actions, actions));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiPlanningState.Restore(roles, roles, families, anchors,
                invalidActions, actions, actions));
        Assert.Throws<ArgumentException>(() =>
            AiPlanningState.Restore(roles, roles, families, anchors,
                actions, actions, actions, new bool[MatchLimits.PlayerCount - 1]));
    }
}

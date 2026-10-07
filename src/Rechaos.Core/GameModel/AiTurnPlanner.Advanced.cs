namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    [Flags]
    internal enum AdvancedFeature
    {
        None = 0,
        IdleRecovery = 1,
        ExpertExpansion = 2,
        All = IdleRecovery | ExpertExpansion
    }

    /// <summary>
    /// Starts with the complete Original AI plan, applies the documented expert
    /// expansion correction, then gives remaining idle gangs one deterministic,
    /// affordable, observable legal command.
    /// </summary>
    internal static IReadOnlyList<GameCommand> PlanAdvanced(
        MatchState state,
        PlayerId playerId) => PlanAdvanced(state, playerId, AdvancedFeature.All);

    internal static IReadOnlyList<GameCommand> PlanAdvanced(
        MatchState state,
        PlayerId playerId,
        AdvancedFeature features)
    {
        var commands = Plan(state, playerId).ToList();
        var player = state.FindPlayer(playerId)!;
        var targets = new CommandOptionCatalog.TargetLists(state);
        if (features.HasFlag(AdvancedFeature.ExpertExpansion))
            ApplyAdvancedExpansion(state, player, commands, targets);
        if (!features.HasFlag(AdvancedFeature.IdleRecovery)) return commands;
        var assigned = commands.Select(command => command.Gang).ToHashSet();
        // The original plan sets no cash aside and may cost more than the player holds
        // (FND-AI-081); the fallback commands only spend what is left after it.
        var cashBudget = Math.Max(0,
            player.Cash - commands.Sum(command => EstimatedCost(state, command)));

        foreach (var gang in player.Gangs.Where(gang => gang.IsActive))
        {
            if (assigned.Contains(gang.Id)) continue;
            var fallback = CommandOptionCatalog.LegalCommands(state, playerId, gang.Id, targets)
                .Where(command => EstimatedCost(state, command) <= cashBudget)
                .Where(command => IsAdvancedFallbackCandidate(state, playerId, gang, command))
                .OrderBy(command => AdvancedActionPriority(command.Action))
                .ThenBy(command => AdvancedTargetPriority(state, command))
                .ThenBy(command => command.Target.Id)
                .ThenBy(command => command.SecondaryTarget?.Id ?? -1)
                .FirstOrDefault();
            if (fallback is null) continue;
            commands.Add(fallback);
            cashBudget -= EstimatedCost(state, fallback);
        }
        return commands;
    }

    private static void ApplyAdvancedExpansion(
        MatchState state,
        MatchPlayerState player,
        List<GameCommand> commands,
        CommandOptionCatalog.TargetLists targets)
    {
        if (state.Setup.AiMentality < AiDifficulty.CrimeLord) return;
        foreach (var entry in player.Gangs.Select((gang, slot) => (gang, slot))
                     .Where(entry => entry.gang.IsActive))
        {
            var gang = entry.gang;
            if (gang.Force < 8 || state.Sectors[gang.SectorId].Owner != player.Id) continue;
            if (!CanReleaseDefender(state, player, gang, commands)) continue;
            var commandIndex = commands.FindIndex(command => command.Gang == gang.Id);
            var original = commandIndex >= 0 ? commands[commandIndex] : null;
            if (original is not null
                && (!IsPassiveExpansionAction(original.Action)
                    || state.AiPlanning.PreviousAction(player.Id, entry.slot) != original.Action))
                continue;

            // Command validation lets a computer player's Move into a full sector through for the
            // Move repair (DEV-MOVE-001); the expansion does not trade a useful order for one.
            // With DEV-AI-007 switched off validation also lets the Move go to any sector, which
            // is for the original planner's Moves; the expansion still steps to a neighbour.
            var move = CommandOptionCatalog.LegalCommands(state, player.Id, gang.Id, targets)
                .Where(command => command.Action == GangAction.Move
                    && CommandValidator.AreNeighbours(gang.SectorId, command.Target.Id)
                    && state.Sectors[command.Target.Id].Owner != player.Id
                    && player.Gangs.Count(candidate => candidate.IsActive
                        && candidate.SectorId == command.Target.Id) < MatchLimits.FriendlyGangsPerSector)
                .OrderByDescending(command => DestinationValue(
                    state, player.Id, command.Target.Id, state.Setup.Scenario))
                .ThenBy(command => command.Target.Id)
                .FirstOrDefault();
            if (move is null) continue;
            if (commandIndex >= 0) commands[commandIndex] = move;
            else commands.Add(move);
        }
    }

    // Repeated Chaos, which families 0 and 4 hold an owned sector with (RULE-AI-019, RULE-AI-023),
    // counts as passive: a defended sector keeps earning from the gangs left in it, and sending the
    // strong ones out wins more territory than it costs
    // (AdvancedAiPlaytestTests.ExpertExpansionImprovesSameSeedPowerCampaignSample).
    private static bool IsPassiveExpansionAction(GangAction action) =>
        action is GangAction.Hide or GangAction.Snitch or GangAction.Bribe or GangAction.Chaos;

    private static bool CanReleaseDefender(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        IReadOnlyList<GameCommand> commands)
    {
        if (state.Players.Where(candidate => candidate.Id != player.Id)
            .SelectMany(candidate => candidate.Gangs)
            .Any(candidate => candidate.IsActive
                && candidate.SectorId == gang.SectorId
                && state.CanPlayerDetectGang(player.Id, candidate.Id)))
            return false;
        var defenders = player.Gangs.Count(candidate =>
            candidate.IsActive && candidate.SectorId == gang.SectorId);
        var outbound = commands.Count(command => command.Action == GangAction.Move
            && player.Gangs.Any(candidate => candidate.Id == command.Gang
                && candidate.IsActive && candidate.SectorId == gang.SectorId));
        return defenders - outbound > 1;
    }

    private static bool IsAdvancedFallbackCandidate(
        MatchState state,
        PlayerId player,
        MatchGangState gang,
        GameCommand command) => command.Action switch
    {
        GangAction.Attack => state.FindGang(new GangId(command.Target.Id)) is { } target
            && target.Owner != player
            && state.CanPlayerDetectGang(player, target.Id),
        GangAction.Heal => gang.Force < ManualRules.MaximumForce,
        GangAction.Control => state.Sectors[gang.SectorId].Owner != player,
        GangAction.Chaos => state.Sectors[gang.SectorId].Owner == player,
        _ => false
    };

    private static int AdvancedActionPriority(GangAction action) => action switch
    {
        GangAction.Control => 0,
        GangAction.Chaos => 1,
        GangAction.Attack => 2,
        GangAction.Heal => 3,
        _ => 4
    };

    private static int AdvancedTargetPriority(MatchState state, GameCommand command) =>
        command.Action == GangAction.Attack
            ? state.FindGang(new GangId(command.Target.Id))?.Force ?? int.MaxValue
            : command.Target.Id;

    /// <summary>
    /// The Advanced policy's ranking of a Move destination: an unowned sector, a Siege or Big Man
    /// objective and income weigh in. The weights are the rebuild's own (DEV-AI-003).
    /// </summary>
    internal static int DestinationValue(
        MatchState state,
        PlayerId player,
        int sectorId,
        ScenarioId scenario)
    {
        var sector = state.Sectors[sectorId];
        var value = sector.Owner == player ? 0 : 100;
        if (scenario == ScenarioId.Siege && sector.IsImportant) value += 300;
        if (scenario == ScenarioId.BigMan && sectorId is 27 or 28 or 35 or 36) value += 300;
        return value + sector.Income * 10;
    }
}

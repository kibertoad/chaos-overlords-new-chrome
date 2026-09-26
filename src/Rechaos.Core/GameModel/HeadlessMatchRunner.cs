using Rechaos.Core.Assets;
using Rechaos.Core.Persistence;

namespace Rechaos.Core.GameModel;

/// <summary>Configuration for one deterministic, presentation-free match simulation.</summary>
/// <remarks>
/// <see cref="SimulatedHumans"/> names seats that register as human but are played by the computer
/// planner (see <see cref="MatchState.SimulateHuman"/>). The journal cannot carry that mark, so a
/// match with simulated humans cannot verify its replay.
/// </remarks>
public sealed record HeadlessMatchOptions(
    ScenarioId Scenario,
    GameDuration Duration,
    int Seed,
    AiPolicyMode AiPolicy = AiPolicyMode.Original,
    int? ThroughTurn = null,
    bool VerifyReplay = false,
    int ProgressEveryTurns = 10,
    IReadOnlyList<PlayerId>? SimulatedHumans = null);

/// <summary>A stable progress point emitted at an upkeep boundary.</summary>
public sealed record HeadlessMatchProgress(
    ScenarioId Scenario,
    int Seed,
    int Turn,
    int PhaseBoundaries,
    int EventCount);

/// <summary>The deterministic result of a headless match simulation.</summary>
public sealed record HeadlessMatchResult(
    MatchState State,
    string StateHash,
    int PhaseBoundaries,
    bool ReplayVerified);

/// <summary>
/// Drives computer-only matches directly through the core model, without constructing the game,
/// graphics, audio, input, animation, or timing layers.
/// </summary>
public static class HeadlessMatchRunner
{
    /// <summary>Runs one match through its requested turn window or natural completion.</summary>
    public static HeadlessMatchResult Run(
        OriginalData definitions,
        HeadlessMatchOptions options,
        Action<HeadlessMatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ThroughTurn is < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "The turn horizon must be positive.");
        if (options.ProgressEveryTurns < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "The progress interval must be positive.");
        var simulatedHumans = options.SimulatedHumans ?? [];
        if (simulatedHumans.Any(player => player.Value is < 0 or >= MatchLimits.PlayerCount)
            || simulatedHumans.Distinct().Count() != simulatedHumans.Count)
            throw new ArgumentOutOfRangeException(nameof(options), "Simulated human seats must be distinct player slots.");
        if (simulatedHumans.Count > 0 && options.VerifyReplay)
            throw new ArgumentException("A match with simulated humans cannot verify its replay.", nameof(options));

        var explicitSeats = Math.Max(2, simulatedHumans.Select(player => player.Value + 1).DefaultIfEmpty(0).Max());
        var players = Enumerable.Range(0, explicitSeats)
            .Select(slot => simulatedHumans.Contains(new PlayerId(slot))
                ? new MatchPlayerSetup(new PlayerId(slot), $"HUMAN {slot + 1}", PlayerController.Human)
                : new MatchPlayerSetup(new PlayerId(slot), $"CPU {slot + 1}", PlayerController.Computer))
            .ToArray();
        var setup = new MatchSetup(
            options.Scenario, options.Duration, options.Seed, players,
            aiPolicy: options.AiPolicy);
        var initial = OriginalMatchFactory.Create(definitions, setup);
        foreach (var player in simulatedHumans) initial.SimulateHuman(player);
        var recorder = new MatchReplayRecorder(initial);
        var boundaries = 0;
        var lastReportedTurn = 0;
        var terminalTurn = options.ThroughTurn ?? ScenarioCatalog.Turns(options.Duration) + 1;
        var boundaryLimit = checked(terminalTurn * 32);

        while (recorder.State.Outcome is null
               && (options.ThroughTurn is null
                   || recorder.State.Coordinator.Turn <= options.ThroughTurn)
               && boundaries++ < boundaryLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = recorder.State;
            if (progress is not null
                && state.Coordinator.Phase == TurnPhase.Upkeep
                && state.Coordinator.Turn % options.ProgressEveryTurns == 0
                && state.Coordinator.Turn != lastReportedTurn)
            {
                lastReportedTurn = state.Coordinator.Turn;
                progress(new HeadlessMatchProgress(
                    options.Scenario, options.Seed, state.Coordinator.Turn,
                    boundaries, state.Events.Count));
            }

            Advance(recorder);
        }

        if (boundaries >= boundaryLimit)
        {
            throw new InvalidOperationException(
                $"Headless match exceeded {boundaryLimit} phase boundaries: "
                + $"scenario={options.Scenario}, seed={options.Seed}, "
                + $"turn={recorder.State.Coordinator.Turn}, "
                + $"phase={recorder.State.Coordinator.Phase}, events={recorder.State.Events.Count}.");
        }

        var stateHash = MatchStateHasher.ComputeFingerprint(recorder.State);
        var replayVerified = false;
        if (options.VerifyReplay)
        {
            using var replay = new MemoryStream();
            MatchReplaySerializer.Save(replay, recorder);
            replay.Position = 0;
            var replayed = MatchReplaySerializer.LoadAndReplay(replay, definitions);
            var replayHash = MatchStateHasher.ComputeFingerprint(replayed);
            if (!string.Equals(stateHash, replayHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Headless replay diverged: scenario={options.Scenario}, seed={options.Seed}, "
                    + $"expected={stateHash}, actual={replayHash}.");
            }
            replayVerified = true;
        }

        return new HeadlessMatchResult(recorder.State, stateHash, boundaries, replayVerified);
    }

    private static void Advance(MatchReplayRecorder recorder)
    {
        var state = recorder.State;
        switch (state.Coordinator.Phase)
        {
            case TurnPhase.Upkeep:
                recorder.FinishUpkeep();
                return;
            case TurnPhase.Command:
                var player = state.Coordinator.ActivePlayer!.Value;
                if (state.FindPlayer(player)?.Status == PlayerStatus.Active)
                    PlanComputerTurn(recorder, player);
                else
                    recorder.FinishCommand(player);
                return;
            case TurnPhase.Execution:
                recorder.FinishExecutionPhase();
                return;
            case TurnPhase.Hire:
                recorder.FinishHire(state.Coordinator.ActivePlayer!.Value);
                return;
            case TurnPhase.PlayerElimination:
                recorder.FinishPlayerElimination();
                return;
            default:
                throw new InvalidOperationException(
                    $"Headless match cannot advance phase {state.Coordinator.Phase}.");
        }
    }

    private static void PlanComputerTurn(MatchReplayRecorder recorder, PlayerId player)
    {
        recorder.PrepareAiPlanning(player);
        foreach (var command in AiPolicyPlanner.Plan(recorder.State, player))
        {
            var result = recorder.Submit(command);
            if (!result.Accepted)
                throw new InvalidOperationException("AI submitted an illegal command.");
        }
        recorder.PrepareHireOffers(player);
        var hiring = recorder.PrepareAiHiring(player);
        if (hiring.Choice is { } choice)
        {
            var result = recorder.QueueHire(player, choice.GangDefinitionId, choice.SectorId);
            if (!result.Accepted)
                throw new InvalidOperationException("AI submitted an illegal hire.");
        }
        else if (hiring.RejectedGangDefinitionId is { } rejectedOffer)
        {
            var result = recorder.SnubHireOffer(player, rejectedOffer);
            if (!result.Accepted)
                throw new InvalidOperationException("AI submitted an illegal snub.");
        }
        recorder.FinishCommand(player);
    }
}

using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> DetailedCombatRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].CombatClips is not null && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-COMBAT-004, RULE-AUDIO-009: with Detailed Combat switched on, the probe recorded every
    // clip the original's presentation played at the human's planning entries (FND-COMBAT-011): the
    // focal gang, the other gang or the police, the hold argument, the right ends of the two bars
    // as the clip starts, and the sound loaded into slot 5 for it (FND-AUDIO-013). At each planning
    // entry of the replay the rebuild's automatic presentation plays the last turn's fights the
    // human can see, and its clips are the same, in the same order. EXP-COMBAT-001 to EXP-COMBAT-005
    // reach an attack by the human's gang, evaded attacks, armed and unarmed attackers, a Martial
    // Arts attacker and the police.
    [Theory]
    [MemberData(nameof(DetailedCombatRuns))]
    public void DetailedCombatPlaysTheOriginalsClips(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var rebuilt = new List<string>();
        void Present(MatchState state, PlayerId human)
        {
            var turn = state.Coordinator.Turn - 1;
            var events = state.Events
                .Where(gameEvent => CombatResultProjection.IsFromLastCompletedTurn(gameEvent.Turn, state.Coordinator.Turn)
                    && CombatResultProjection.IsVisibleCombatEvent(state, human, gameEvent))
                .ToArray();
            foreach (var clip in CombatAnimationRouting.ForPresentation(state, events, human))
                rebuilt.Add(Describe(turn, state, human, clip));
        }
        var human = recorded.Humans[0];
        var match = StartMatch(recorded, out _, (state, player, turn) =>
        {
            if (turn > 1) Present(state, player);
        });
        // An eliminated human gets the elimination card and an ended match the endgame, in place of
        // the planning entry that would have opened the presentation.
        if (IsActive(match, human) && match.Outcome is null) Present(match, human);

        var original = recorded.CombatClips!.Select(clip => Describe(
            recorded.DoneAtRoll.Count(done => done < clip.AfterRoll), clip.Focal, clip.Other, clip.Hold,
            clip.FocalBar, clip.OtherBar, clip.Sounds, clip.Played)).ToArray();
        Assert.Equal(original, rebuilt);
    }

    // The original sets the focal bar's right end to 256 + 6 * force_shown and the other's to
    // 329 + 6 * force_shown, and loads slot 5 with sound 500 + the clip's sound number
    // (FND-COMBAT-011, FND-AUDIO-013).
    private static string Describe(int turn, MatchState state, PlayerId viewer, CombatAnimationClip clip)
    {
        var gameEvent = state.Events.Single(candidate => candidate.Sequence == clip.EventSequence);
        int Element(CombatantDetails gang) => gang.Owner.Value * AiPlanningState.GangSlotsPerPlayer + gang.RosterSlot!.Value;
        int focal, other, focalForce, otherForce;
        if (clip.Police)
        {
            focal = Element(gameEvent.PoliceAttack!.Target!);
            other = -2;
            focalForce = clip.Forces.DefenderBefore;
            otherForce = 10;
        }
        else
        {
            var attacker = gameEvent.Resolution!.Attacker!;
            var defender = gameEvent.Resolution.Defender!;
            var outgoing = attacker.Owner == viewer;
            focal = Element(outgoing ? attacker : defender);
            other = Element(outgoing ? defender : attacker);
            focalForce = outgoing ? clip.Forces.AttackerBefore!.Value : clip.Forces.DefenderBefore;
            otherForce = outgoing ? clip.Forces.DefenderBefore : clip.Forces.AttackerBefore!.Value;
        }
        return Describe(turn, focal, other, clip.HandsOff ? 0 : 1, 256 + 6 * focalForce, 329 + 6 * otherForce,
            [500 + (clip.Sound ?? -1)], true);
    }

    private static string Describe(
        int turn, int focal, int other, int hold, int focalBar, int otherBar, IReadOnlyList<int> sounds, bool played) =>
        $"turn {turn}: focal {focal} other {other} hold {hold} bars {focalBar},{otherBar} sounds [{string.Join(",", sounds)}] played {played}";
}

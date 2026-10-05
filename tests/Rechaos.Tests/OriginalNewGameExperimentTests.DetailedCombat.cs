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
    // human can see, and its clips are the same, in the same order. EXP-COMBAT-001 to EXP-COMBAT-009
    // reach an attack by the human's gang, evaded attacks on it and by it, armed and unarmed
    // attackers, a Martial Arts attacker, two gangs attacking each other, the police and fights in
    // two sectors.
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

        // The clips of presentations the console opened are compared by
        // TheConsolesDetailedCombatControlPlaysTheLastTurnAgain.
        var console = (recorded.CombatPresentations ?? []).Where(presentation => !presentation.Automatic)
            .SelectMany(presentation => Enumerable.Range(presentation.FirstClip, presentation.Clips)).ToHashSet();
        Assert.All((recorded.CombatPresentations ?? []).Where(presentation => presentation.Automatic),
            presentation => Assert.Empty(presentation.Sounds));
        var original = recorded.CombatClips!.Where((_, index) => !console.Contains(index)).Select(clip => Describe(
            recorded.DoneAtRoll.Count(done => done < clip.AfterRoll), clip.Focal, clip.Other, clip.Hold,
            clip.FocalBar, clip.OtherBar, clip.Sounds, clip.Played)).ToArray();
        Assert.Equal(original, rebuilt);
    }

    public static TheoryData<string, int> ConsoleDetailedCombatRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].CombatPresentations?.Any(presentation => !presentation.Automatic) == true)
                    data.Add(experiment, run);
        return data;
    }

    // RULE-COMBAT-004, SCR-UI-003, SCR-COMBAT-002: after the dump the probe pressed the console's
    // Detailed Combat control, which calls the presentation with its flag 0 (FND-COMBAT-010). With
    // no fight of the viewer's in the last turn the original plays effect slot 4 and shows nothing,
    // as the rebuild's control plays its rejected sound, slot 4, for an empty presentation. With
    // fights it plays the last turn's clips again from the first, the rebuild's clips, and a release
    // on the panel's Exit face during the first clip ends the presentation there: the original
    // played only that clip, and the rebuild's Exit ends the whole presentation (EXP-COMBAT-006,
    // EXP-COMBAT-008).
    [Theory]
    [MemberData(nameof(ConsoleDetailedCombatRuns))]
    public void TheConsolesDetailedCombatControlPlaysTheLastTurnAgain(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var human = recorded.Humans[0];
        var events = match.Events
            .Where(gameEvent => CombatResultProjection.IsFromLastCompletedTurn(gameEvent.Turn, match.Coordinator.Turn)
                && CombatResultProjection.IsVisibleCombatEvent(match, human, gameEvent))
            .ToArray();
        var clips = CombatAnimationRouting.ForPresentation(match, events, human);
        var turn = match.Coordinator.Turn - 1;
        var steps = recorded.OrderSteps.Where(step => step.Kind == "strip").ToArray();
        Assert.True(CityConsoleLayout.CombatDetail.Contains(steps[0].X, steps[0].Y));

        var presentation = Assert.Single(recorded.CombatPresentations!, presentation => !presentation.Automatic);
        if (clips.Count == 0)
        {
            Assert.Equal(0, presentation.Clips);
            Assert.Equal([GeneralSoundSlot.RejectedInput], presentation.Sounds);
            return;
        }
        Assert.Empty(presentation.Sounds);
        var played = recorded.CombatClips!.Skip(presentation.FirstClip).Take(presentation.Clips).Select(clip => Describe(
            recorded.DoneAtRoll.Count(done => done < clip.AfterRoll), clip.Focal, clip.Other, clip.Hold,
            clip.FocalBar, clip.OtherBar, clip.Sounds, clip.Played)).ToArray();
        Assert.Equal(clips.Take(played.Length).Select(clip => Describe(turn, match, human, clip)), played);
        if (played.Length < clips.Count)
        {
            var exit = steps[1];
            var face = new DetailedCombatExit();
            var point = new Microsoft.Xna.Framework.Point(exit.X, exit.Y);
            Assert.Equal(DetailedCombatPointerResult.None, face.Update(point, down: true, wasDown: false));
            Assert.Equal(DetailedCombatPointerResult.EndPresentation, face.Update(point, down: false, wasDown: true));
        }
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

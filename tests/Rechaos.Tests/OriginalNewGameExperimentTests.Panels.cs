using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> PanelRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, run) in PanelRunKeys()) data.Add(experiment, run);
        return data;
    }

    private static IEnumerable<(string Experiment, int Run)> PanelRunKeys()
    {
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                // The probe breaks on the handlers at every local human's planning entry, and the
                // replay compares only the first human's, so a run with several humans is left out.
                // With Detailed Combat switched on the planning entry calls the presentation in place
                // of the Combat Results handler (FND-COMBAT-010), so a run that recorded clips is
                // left out too; DetailedCombatPlaysTheOriginalsClips compares those entries.
                if (runs[run].Panels is not null && runs[run].Humans.Count == 1 && runs[run].CombatClips is null
                    && !KnownDivergences.ContainsKey((experiment, run)))
                    yield return (experiment, run);
    }

    // RULE-SETUP-008, RULE-EVENT-005: the probe records each call of the Combat Results and Last
    // Turn Events panels at the human's planning entries, and whether the panel stayed open until
    // Exit was pressed; Combat Results returns at once when no fight qualifies. At each planning
    // entry the rebuild shows Combat Results when the viewer has combat results and then Last Turn
    // Events when the viewer has reports, or the city when neither applies, and the panels it
    // shows match the original's at that entry, in the same order.
    [Theory]
    [MemberData(nameof(PanelRuns))]
    public void ThePlanningEntryShowsTheOriginalsPanels(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var expected = new List<string>[recorded.DoneCount + 1];
        var combatCalled = new bool[recorded.DoneCount + 1];
        for (var entry = 0; entry < expected.Length; entry++) expected[entry] = [];
        foreach (var call in recorded.Panels!)
        {
            // Entry e follows e Done presses. The resolution after a press makes rolls, so its calls
            // come after the roll count of press e and no later than that of press e + 1.
            var entry = recorded.DoneAtRoll.Count(count => count < call.AfterRoll);
            if (call.Panel == "Combat Results") combatCalled[entry] = true;
            if (call.Shown) expected[entry].Add(call.Panel);
        }
        // The original calls the Combat Results handler at every planning entry, so an entry
        // without that call is one the probe did not observe, and comparing it would prove nothing.
        // Each entry before a Done press is a planning entry; the last is checked below.
        for (var entry = 0; entry < recorded.DoneCount; entry++)
            Assert.True(combatCalled[entry], $"planning entry {entry + 1}: the recording holds no call of Combat Results");

        var (shown, donePresses, endsAtPlanningEntry, lastEntryFailure) = PanelReplays.Get((experiment, run));
        // An early stop would leave the recording's later entries uncompared.
        Assert.Equal(recorded.DoneCount, donePresses);
        // The last entry is a planning entry unless the match ended or the human was eliminated. An
        // eliminated human has none, and the final view of an ended match is closed by the probe
        // with every panel it opens, so neither is compared.
        var compared = donePresses;
        if (endsAtPlanningEntry)
        {
            Assert.True(combatCalled[donePresses], $"planning entry {donePresses + 1}: the recording holds no call of Combat Results");
            lastEntryFailure?.Throw();
            // The run stops at the last entry while its first panel is open, so only that panel is seen.
            if (expected[donePresses].Count > 0) shown[donePresses] = shown[donePresses].Take(1).ToList();
            compared++;
        }
        for (var entry = 0; entry < compared; entry++)
            Assert.True(expected[entry].SequenceEqual(shown[entry]),
                $"planning entry {entry + 1}: the original showed [{string.Join(", ", expected[entry])}], the rebuild [{string.Join(", ", shown[entry])}]");
    }

    // The panels the rebuild shows at each planning entry of a run, the Done presses its replay
    // made, and whether it ends at a planning entry, whose panels are then the last ones. Each run
    // is played again with its own game, so the rows replay theirs ahead on a few workers.
    private sealed record PanelReplay(
        List<string>[] Shown, int DonePresses, bool EndsAtPlanningEntry, ExceptionDispatchInfo? LastEntryFailure);

    private static readonly RowPrefetch<(string Experiment, int Run), PanelReplay> PanelReplays =
        new(PanelRunKeys, key => ReplayPanels(Run(key.Experiment, key.Run)));

    private static PanelReplay ReplayPanels(RecordedRun recorded)
    {
        var shown = new List<string>[recorded.DoneCount + 1];
        using var game = new HeadlessGame(HeadlessGame.DefaultPreferences with { DetailedCombat = false });
        // The panels are read at each planning entry, before any of that turn's recorded orders,
        // hires or planning writes reach the state, as the original showed them.
        var match = StartMatch(recorded, out var donePresses,
            atPlanningEntry: (state, human, turn) => shown[turn - 1] = PlanningEntryPanels(game, state, human));
        var endsAtPlanningEntry = match.Outcome is null && IsActive(match, recorded.Humans[0]);
        // A replay that stopped early fails its row before the last entry is read.
        // A failure reading it is held for the row, which first checks that the recording holds
        // that entry's call of Combat Results.
        ExceptionDispatchInfo? lastEntryFailure = null;
        if (endsAtPlanningEntry && donePresses == recorded.DoneCount)
        {
            try
            {
                shown[donePresses] = PlanningEntryPanels(game, match, recorded.Humans[0]);
            }
            catch (Exception exception)
            {
                lastEntryFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }
        return new PanelReplay(shown, donePresses, endsAtPlanningEntry, lastEntryFailure);
    }

    /// <summary>
    /// The panels the rebuild shows at <paramref name="match"/>'s planning entry, in order: a copy of
    /// the match is put on screen in <paramref name="game"/> as a hot-seat planning entry, and each
    /// panel it opens is closed with Enter until the city shows.
    /// </summary>
    private static List<string> PlanningEntryPanels(HeadlessGame game, MatchState match, PlayerId human)
    {
        using var copy = new MemoryStream();
        NativeSaveSerializer.Save(copy, match);
        copy.Position = 0;
        game.Game.EnterPlanningEntry(NativeSaveSerializer.Load(copy, match.Definitions));
        Assert.Equal(human, game.Game.Match!.Coordinator.ActivePlayer);
        var panels = new List<string>();
        while (game.Game.CurrentScreen != ClientScreen.City)
        {
            panels.Add(game.Game.CurrentScreen switch
            {
                ClientScreen.CombatSummary => "Combat Results",
                ClientScreen.Events => "Last Turn Events",
                var other => throw new InvalidOperationException($"the planning entry showed {other}"),
            });
            Assert.True(panels.Count <= 2, $"the planning entry showed [{string.Join(", ", panels)}] and more");
            game.Press(Keys.Enter);
        }
        return panels;
    }
}

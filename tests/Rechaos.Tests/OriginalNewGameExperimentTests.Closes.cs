using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> CloseRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Closes.Count > 0)
                    data.Add(experiment, run);
        return data;
    }

    // RULE-UI-015, FND-UI-058: the probe read the original's saved byte before each write it made
    // (EXP-UI-026), in the new match's first planning and after turn 1 resolved, then dragged a hire
    // offer and closed the window twice. The rebuild's saved mark gives the same value at each read,
    // a hire clears it, and closing the window asks to save first exactly when the original opened
    // dialog 129. Each write of 1 is a save; the rebuild has no other way to set the mark.
    [Theory]
    [MemberData(nameof(CloseRuns))]
    public void ClosingAsksToSaveExactlyWhileTheMatchIsUnsaved(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        MatchActions? actions = null;
        void Write(MatchState match, int turn)
        {
            actions ??= new MatchActions(MatchReplayRecorder.Unverified(match));
            foreach (var write in recorded.SavedWrites.Where(write => write.Turn == turn))
            {
                Assert.True(write.Before == (actions.IsSaved ? 1 : 0),
                    $"turn {turn}: the original's byte was {write.Before} before the write");
                Assert.Equal(1, write.Value);
                actions.MarkSaved();
            }
        }
        var match = StartMatch(recorded, out _, atPlanningEntry: (state, _, turn) => Write(state, turn));
        Write(match, recorded.DoneCount + 1);

        var human = recorded.Humans[0];
        var player = match.FindPlayer(human)!;
        foreach (var step in recorded.HireSteps.Where(step => step.Slot >= 0 && step.Sector >= 0))
            if (HireDropPlacement.Rejection(match, human, step.Sector) is null)
                Assert.True(actions!.QueueHire(human, player.HireOfferSlots[step.Slot].GangDefinitionId!.Value, step.Sector).Accepted);

        var game = LeavePromptTests.GameWith(match, actions!);
        foreach (var close in recorded.Closes)
        {
            if (close.Saved == 1 && !actions!.IsSaved) actions.MarkSaved();
            Assert.Equal(close.Saved == 1, actions!.IsSaved);
            // The original's no_match_in_play byte: the rebuild has a match in play while it has
            // a match whose end evaluation has not run.
            Assert.Equal(close.NoMatch != 0, match.Outcome is not null);
            var asked = close.Dialogs.Contains(129);
            Assert.Equal(asked, LeavePromptTests.ClosingIsCancelled(game));
            Assert.Equal(asked ? LeaveKind.Exit : LeaveKind.None,
                (LeaveKind)DeviationBehaviourTests.Field("_leavePrompt").GetValue(game)!);
            if (!asked) Assert.True(close.QuitRequested);
            else if (close.Answer == (int)LeaveAnswer.Cancel + 1)
            {
                Assert.False(close.QuitRequested);
                DeviationBehaviourTests.Call(game, "AnswerLeavePrompt", LeaveAnswer.Cancel);
                Assert.Equal(LeaveKind.None, (LeaveKind)DeviationBehaviourTests.Field("_leavePrompt").GetValue(game)!);
                Assert.False((bool)DeviationBehaviourTests.Field("_gameMenuOpen").GetValue(game)!);
            }
        }
    }
}

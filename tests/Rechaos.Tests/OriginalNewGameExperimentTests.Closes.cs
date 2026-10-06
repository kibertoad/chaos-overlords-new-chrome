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
    // offer and closed the window five times, giving each answer of dialog 129 and both results of
    // the save the first answer starts. The rebuild's saved mark gives the same value at each read,
    // a hire clears it, closing the window asks to save first exactly when the original opened
    // dialog 129, and each answer opens the save browser and leaves exactly when the original called
    // its save and stored quit_requested. Each write of 1 is a save; the rebuild has no other way to
    // set the mark. A leave is the rebuild's confirmed exit, which the original's skipped store
    // stands for. Closing the window is File, Exit (RULE-UI-014), which LeavePromptTests.ClosingIsCancelled
    // drives through the game's exiting handler.
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

        var directory = Directory.CreateTempSubdirectory("rechaos-exp-ui-026-");
        try
        {
            foreach (var close in recorded.Closes)
            {
                var game = LeavePromptTests.GameFor(match, actions!, directory.FullName);
                if (close.Saved == 1 && !actions!.IsSaved) actions.MarkSaved();
                Assert.Equal(close.Saved == 1, actions!.IsSaved);
                Assert.Equal(0, close.NoMatch);
                var asked = close.Dialogs.Contains(129);
                var held = LeavePromptTests.ClosingIsCancelled(game);
                Assert.Equal(asked, held);
                Assert.Equal(asked ? LeaveKind.Exit : LeaveKind.None,
                    (LeaveKind)DeviationBehaviourTests.Field("_leavePrompt").GetValue(game)!);
                if (asked)
                {
                    var answer = (LeaveAnswer)(close.Answer - 1);
                    LeavePromptTests.Answer(game, answer);
                    var browser = (SaveBrowserMode)DeviationBehaviourTests.Field("_saveBrowserMode").GetValue(game)!;
                    Assert.Equal(close.Saves > 0, browser == SaveBrowserMode.Save);
                    if (answer == LeaveAnswer.SaveFirst)
                    {
                        if (close.SaveResult == 1) LeavePromptTests.SaveWritten(game);
                        else DeviationBehaviourTests.Call(game, "CloseSaveBrowser");
                    }
                }
                Assert.Equal(close.Left, !held || LeavePromptTests.Left(game));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

using System.Text.Json;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What leaves the machine when a player ticks "share anonymized game state".
/// </summary>
public sealed class ReplayAnonymizerTests
{
    [Fact]
    public void ReplacesPlayerNamesWithSeatLabels()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET"));
        recorder.FinishUpkeep();

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        Assert.Equal(
            ["PLAYER 1", "PLAYER 2"],
            anonymized.State.Setup.Players.Select(player => player.Name));
        Assert.DoesNotContain("MARGARET", SerializeJournal(anonymized), StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacesTheTextOfAMessageSentDuringTheJournal()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create(secondPlayerHuman: true));
        recorder.FinishUpkeep();
        Assert.True(recorder.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "MY ADDRESS IS 14 ELM STREET").Accepted);

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        var delivered = anonymized.State.ComlinkFor(new PlayerId(1)).Messages;
        Assert.Equal(
            new string('X', "MY ADDRESS IS 14 ELM STREET".Length),
            Assert.Single(delivered).Text);
        Assert.DoesNotContain("ELM STREET", SerializeJournal(anonymized), StringComparison.Ordinal);
    }

    /// <summary>
    /// A message already in an inbox when the journal starts is in the snapshot, not in a step.
    /// </summary>
    /// <remarks>
    /// That is the ordinary shape after a save and a load, and it is the half a rewrite that only
    /// touched the recorded operations would miss entirely.
    /// </remarks>
    [Fact]
    public void ReplacesTheTextOfAMessageAlreadyInTheOpeningSnapshot()
    {
        var earlier = new MatchReplayRecorder(TestMatches.Create("MARGARET", secondPlayerHuman: true));
        earlier.FinishUpkeep();
        Assert.True(earlier.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "MY ADDRESS IS 14 ELM STREET").Accepted);
        // A fresh journal over the same match: everything so far is now in its opening snapshot.
        var recorder = new MatchReplayRecorder(earlier.State);

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        Assert.Equal(
            new string('X', "MY ADDRESS IS 14 ELM STREET".Length),
            Assert.Single(anonymized.State.ComlinkFor(new PlayerId(1)).Messages).Text);
        Assert.Equal("PLAYER 1", anonymized.State.Setup.Players[0].Name);
        Assert.DoesNotContain("ELM STREET", SerializeJournal(anonymized), StringComparison.Ordinal);
        Assert.DoesNotContain("MARGARET", SerializeJournal(anonymized), StringComparison.Ordinal);
    }

    /// <summary>
    /// The anonymized journal has to be a journal: every fingerprint in it recomputed, so it
    /// replays end to end on somebody else's machine.
    /// </summary>
    [Fact]
    public void ProducesAJournalThatStillReplays()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET", secondPlayerHuman: true));
        recorder.FinishUpkeep();
        Assert.True(recorder.SendComlinkMessage(new PlayerId(0), [new PlayerId(1)], "HELLO").Accepted);
        foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        var anonymized = ReplayAnonymizer.Anonymize(recorder);
        using var journal = new MemoryStream();
        MatchReplaySerializer.Save(journal, anonymized);
        journal.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(journal, recorder.State.Definitions);

        Assert.Equal(recorder.Steps.Count, anonymized.Steps.Count);
        Assert.Equal(
            MatchStateHasher.ComputeSha256(anonymized.State),
            MatchStateHasher.ComputeSha256(replayed));
    }

    /// <summary>
    /// Anonymizing must not change how the match played out, only what the players are called.
    /// </summary>
    [Fact]
    public void KeepsTheMatchItself()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET"));
        recorder.FinishUpkeep();
        foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        Assert.Equal(recorder.State.Coordinator.Turn, anonymized.State.Coordinator.Turn);
        Assert.Equal(recorder.State.Coordinator.Phase, anonymized.State.Coordinator.Phase);
        Assert.Equal(recorder.State.Random.State, anonymized.State.Random.State);
        Assert.Equal(
            JsonSerializer.Serialize(recorder.State.Events),
            JsonSerializer.Serialize(anonymized.State.Events));
        Assert.Equal(
            recorder.Steps.Select(step => step.Kind),
            anonymized.Steps.Select(step => step.Kind));
    }

    /// <summary>
    /// A name the original reads as a cheat code is a rule, not a person, and stays.
    /// </summary>
    /// <remarks>
    /// Substituting one would change what the match does — omniscience is read every turn — and the
    /// re-run would diverge instead of anonymizing. Nothing about a fixed 1996 cheat string says who
    /// typed it.
    /// </remarks>
    [Fact]
    public void LeavesACheatCodeNameAlone()
    {
        var cheat = ReservedPlayerNames.All[0];
        var recorder = new MatchReplayRecorder(TestMatches.Create(cheat));
        recorder.FinishUpkeep();

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        Assert.Equal(cheat, anonymized.State.Setup.Players[0].Name);
        Assert.Equal("PLAYER 2", anonymized.State.Setup.Players[1].Name);
    }

    [Fact]
    public void AnonymizesAnEmptyJournal()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET"));

        var anonymized = ReplayAnonymizer.Anonymize(recorder);

        Assert.Empty(anonymized.Steps);
        Assert.Equal("PLAYER 1", anonymized.State.Setup.Players[0].Name);
    }

    private static string SerializeJournal(MatchReplayRecorder recorder)
    {
        using var stream = new MemoryStream();
        MatchReplaySerializer.Save(stream, recorder);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>The anonymized journal could not be produced, so nothing is sent.</summary>
/// <remarks>
/// Always a refusal rather than a degraded result. The alternative to an anonymized journal is not
/// a slightly-less-anonymized one, it is no journal at all — so anything unexpected here ends with
/// the player's description sent on its own.
/// </remarks>
public sealed class ReplayAnonymizationException : Exception
{
    public ReplayAnonymizationException(string message) : base(message)
    {
    }

    public ReplayAnonymizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Rewrites the two things in a match journal that are a person rather than a game.
/// </summary>
/// <remarks>
/// <para>
/// A journal is the whole reproduction of a bug, which is why it is worth attaching to a report at
/// all. It is also, as recorded, a file with the names of everyone at the table in it and every
/// Comlink message they sent each other. Those are the only two, and both are replaced here: names
/// become seat labels, message text becomes a run of the same length.
/// </para>
/// <para>
/// Neither can simply be edited in place. Both are part of the canonical state hash, so a journal
/// with substituted text would fail at its first step. What happens instead is a re-run: the
/// snapshot is rewritten, the match is restored from it, and every recorded operation is applied to
/// that match through a fresh recorder, which computes the fingerprints as it goes. The result is a
/// genuine, self-consistent journal of the same game played by differently named players.
/// </para>
/// <para>
/// Every step's outcome is compared with the one originally recorded, and a single disagreement
/// abandons the whole thing. That check is not a formality: a handful of original player names are
/// cheat codes the rules read at runtime (see <see cref="ReservedPlayerNames"/>), so those are
/// deliberately left alone — a cheat code is a game rule, not somebody's name. The comparison is
/// what proves nothing else has crept into the same category.
/// </para>
/// </remarks>
public static class ReplayAnonymizer
{
    /// <summary>What a Comlink message becomes: the same length, none of the content.</summary>
    private const char RedactedCharacter = 'X';

    /// <summary>The seat label a player's name is replaced with, 1-based as the interface counts.</summary>
    public static string SeatName(int playerId) => $"PLAYER {playerId + 1}";

    /// <summary>
    /// Answers a recorder holding an anonymized journal of the same match.
    /// </summary>
    /// <exception cref="ReplayAnonymizationException">
    /// The rewritten journal did not reproduce the original's outcomes, or could not be built.
    /// </exception>
    public static MatchReplayRecorder Anonymize(MatchReplayRecorder source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var document = source.Capture();
        MatchState state;
        try
        {
            using var snapshot = new MemoryStream(
                RewriteSnapshot(document.InitialSnapshot), writable: false);
            state = NativeSaveSerializer.LoadRewritten(snapshot, source.State.Definitions);
        }
        // `RewriteSnapshot` reaches into the JSON with `AsObject`, `AsArray` and `GetValue<T>`,
        // which answer a shape mismatch with InvalidOperationException or FormatException. The
        // input is this build's own snapshot, so neither should happen — but `BugReportComposer`
        // filters for ReplayAnonymizationException and InvalidDataException, so one of those
        // escaping would crash the report dialog rather than degrading to "not anonymizable".
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or InvalidOperationException or FormatException)
        {
            throw new ReplayAnonymizationException(
                "The match snapshot could not be rewritten without its names.", exception);
        }

        var rebuilt = new MatchReplayRecorder(state);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            try
            {
                Reapply(rebuilt, document.Steps[index], index);
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException or KeyNotFoundException or OverflowException)
            {
                throw new ReplayAnonymizationException(
                    $"Anonymized replay could not apply step {index}.", exception);
            }
        }
        return rebuilt;
    }

    /// <summary>
    /// Replaces the names and the Comlink text inside a native snapshot.
    /// </summary>
    /// <remarks>
    /// Edited as a JSON tree rather than through the save records, because those are shaped by what
    /// the game needs to restore and this needs to touch two fields and leave every other byte —
    /// including the parts of the document this build has no opinion about — exactly as it found
    /// them. Both paths are required to exist: a snapshot that has stopped having a player roster
    /// where one is expected is a schema change that must fail loudly here rather than quietly send
    /// names.
    /// </remarks>
    private static byte[] RewriteSnapshot(byte[] snapshot)
    {
        var root = JsonNode.Parse(snapshot)?.AsObject()
            ?? throw new InvalidDataException("Match snapshot is not a JSON object.");

        var setup = Required(root, "setup").AsObject();
        var roster = Required(setup, "players").AsArray();
        foreach (var entry in roster)
        {
            var player = entry?.AsObject()
                ?? throw new InvalidDataException("Match snapshot has an empty roster entry.");
            var id = Required(player, "id").GetValue<int>();
            var name = Required(player, "name").GetValue<string>();
            player["name"] = AnonymizedName(id, name);
        }

        var runtime = root["runtime"]?.AsObject();

        // The phase-boundary fingerprints are digests of the match as it was *named*, and there is
        // no way to rewrite them: recomputing one needs the state it was taken over, and those
        // states are behind the journal's first step. Carrying them would be wrong twice — they
        // describe a match nobody is being sent, and they answer "was this player called X?" for
        // anyone willing to try a name — so the history starts empty and the replay rebuilds it.
        // A journal recorded from the first turn has none of these; one resumed mid-match does.
        if (runtime?["phaseHashes"] is not null) runtime["phaseHashes"] = new JsonArray();

        // Comlink arrived in save format 17; an older snapshot legitimately has no inboxes at all.
        if (runtime?["comlink"] is JsonArray inboxes)
        {
            foreach (var inbox in inboxes)
            {
                if (inbox?.AsObject()["items"] is not JsonArray items) continue;
                foreach (var item in items)
                {
                    var message = item?.AsObject()
                        ?? throw new InvalidDataException("Match snapshot has an empty message.");
                    message["text"] = Redact(Required(message, "text").GetValue<string>());
                }
            }
        }
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    /// <summary>Applies one recorded operation to the rebuilt journal, with its text redacted.</summary>
    private static void Reapply(MatchReplayRecorder recorder, ReplayStep step, int index)
    {
        switch (step.Kind)
        {
            case ReplayOperationKind.SubmitCommand:
                Verify(step, recorder.Submit(Required(step.Command, index)), index);
                break;
            case ReplayOperationKind.CancelCommand:
                Verify(step, recorder.Cancel(RequiredValue(step.Player, index), RequiredValue(step.Gang, index)), index);
                break;
            case ReplayOperationKind.QueueHire:
                Verify(step, recorder.QueueHire(
                    RequiredValue(step.Player, index),
                    RequiredValue(step.GangDefinitionId, index),
                    RequiredValue(step.SectorId, index)), index);
                break;
            case ReplayOperationKind.SnubHireOffer:
                Verify(step, recorder.SnubHireOffer(
                    RequiredValue(step.Player, index), RequiredValue(step.GangDefinitionId, index)), index);
                break;
            case ReplayOperationKind.FinishUpkeep: recorder.FinishUpkeep(); break;
            case ReplayOperationKind.FinishCommand:
                recorder.FinishCommand(RequiredValue(step.Player, index));
                break;
            case ReplayOperationKind.FinishExecutionPhase: recorder.FinishExecutionPhase(); break;
            case ReplayOperationKind.FinishHire: recorder.FinishHire(RequiredValue(step.Player, index)); break;
            case ReplayOperationKind.FinishPlayerElimination: recorder.FinishPlayerElimination(); break;
            case ReplayOperationKind.DismissNotification:
                VerifyOutcome(
                    step.Accepted,
                    recorder.TryDismissNotification(RequiredValue(step.Player, index), out _),
                    index);
                break;
            case ReplayOperationKind.PrepareHireOffers:
                recorder.PrepareHireOffers(RequiredValue(step.Player, index));
                break;
            case ReplayOperationKind.PrepareSimultaneousHireOffers:
                recorder.PrepareSimultaneousHireOffers();
                break;
            case ReplayOperationKind.PrepareAiPlanning:
                recorder.PrepareAiPlanning(RequiredValue(step.Player, index));
                break;
            case ReplayOperationKind.PrepareAiHiring:
                recorder.PrepareAiHiring(RequiredValue(step.Player, index));
                break;
            case ReplayOperationKind.SendComlinkMessage:
            {
                var result = recorder.SendComlinkMessage(
                    RequiredValue(step.Player, index),
                    Required(step.Recipients, index),
                    Redact(Required(step.Text, index)));
                Verify(step, result.Accepted, (int)result.Code, index);
                break;
            }
            case ReplayOperationKind.MarkComlinkRead:
                VerifyOutcome(
                    step.Accepted,
                    recorder.MarkComlinkRead(
                        RequiredValue(step.Player, index), RequiredValue(step.ComlinkSequence, index)),
                    index);
                break;
            // The two seat transfers. `MatchReplaySerializer.ApplyStep` has handled them since
            // online play recorded them; this did not, so a journal carrying either ended in "has
            // an operation this build cannot re-apply" and the bug report went out with no state.
            // Not reachable while reports attach the speculative turn's journal, and live the
            // moment a session journal is attached to one.
            case ReplayOperationKind.TransferPlayerToComputer:
                VerifyOutcome(
                    step.Accepted,
                    recorder.TransferPlayerToComputer(RequiredValue(step.Player, index)),
                    index);
                break;
            case ReplayOperationKind.TransferPlayerToHuman:
                VerifyOutcome(
                    step.Accepted,
                    recorder.TransferPlayerToHuman(RequiredValue(step.Player, index)),
                    index);
                break;
            case ReplayOperationKind.ContinueRandomStream:
                recorder.ContinueRandomStream(RequiredValue(step.RandomState, index));
                break;
            case ReplayOperationKind.EmptyComlinkInboxes:
                VerifyOutcome(step.Accepted, recorder.EmptyComlinkInboxes(), index);
                break;
            default:
                throw new ReplayAnonymizationException(
                    $"Journal step {index} has an operation this build cannot re-apply.");
        }
    }

    /// <summary>
    /// A name, unless it is a rule.
    /// </summary>
    /// <remarks>
    /// Six exact names are cheat codes in the original, read at setup and during play — one of them
    /// decides what a player can see every turn. Replacing one would change how the match plays and
    /// the re-run would diverge, so they stay. Nothing is leaked by that: they are fixed strings
    /// from a 1996 game, identical for everyone who types one, and they say nothing about who did.
    /// </remarks>
    private static string AnonymizedName(int playerId, string name) =>
        ReservedPlayerNames.IsReserved(name) ? name : SeatName(playerId);

    private static string Redact(string text) => new(RedactedCharacter, text.Length);

    private static JsonNode Required(JsonObject parent, string property) =>
        parent[property] ?? throw new InvalidDataException(
            $"Match snapshot has no \"{property}\"; the anonymizer cannot rewrite it.");

    private static T Required<T>(T? value, int index) where T : class =>
        value ?? throw new ReplayAnonymizationException($"Journal step {index} is incomplete.");

    private static T RequiredValue<T>(T? value, int index) where T : struct =>
        value ?? throw new ReplayAnonymizationException($"Journal step {index} is incomplete.");

    private static void Verify(ReplayStep step, CommandSubmissionResult result, int index) =>
        Verify(step, result.Accepted, (int)result.Validation.Code, index);

    private static void Verify(ReplayStep step, HireSubmissionResult result, int index) =>
        Verify(step, result.Accepted, (int)result.Validation.Code, index);

    private static void Verify(ReplayStep step, HireOfferSnubResult result, int index) =>
        Verify(step, result.Accepted, (int)result.Validation.Code, index);

    private static void Verify(ReplayStep step, bool accepted, int validationCode, int index)
    {
        VerifyOutcome(step.Accepted, accepted, index);
        if (step.ValidationCode != validationCode)
            throw new ReplayAnonymizationException(
                $"Anonymized replay judged step {index} differently.");
    }

    private static void VerifyOutcome(bool? expected, bool actual, int index)
    {
        if (expected is { } wanted && wanted != actual)
            throw new ReplayAnonymizationException(
                $"Anonymized replay produced a different outcome at step {index}.");
    }
}

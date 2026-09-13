using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>What happened to the journal the player asked to include.</summary>
public enum BugReportStateOutcome
{
    /// <summary>The player left the box unticked, or there was no match to record.</summary>
    NotRequested,

    /// <summary>Anonymized, compressed and attached.</summary>
    Attached,

    /// <summary>The journal could not be anonymized, so none was attached.</summary>
    NotAnonymizable,

    /// <summary>Even compressed, the journal is past what the server will take.</summary>
    TooLarge
}

/// <summary>A report ready to post, and what became of its journal.</summary>
public sealed record ComposedBugReport(
    SubmitBugReportRequest Request,
    BugReportStateOutcome StateOutcome,
    int CompressedBytes);

/// <summary>
/// A match lifted out of the live objects, so the slow work can happen somewhere else.
/// </summary>
/// <remarks>
/// Anonymizing replays a match, and a replay reads the recorder — so it cannot run while the player
/// is still mutating one. Everything here is therefore taken on the thread that owns the match, and
/// none of it can change afterwards: the journal is bytes, the context is already the values the
/// report will carry, and the definitions are immutable.
/// </remarks>
public sealed record CapturedJournal(
    byte[] Json,
    OriginalData Definitions,
    BugReportContext Context);

/// <summary>
/// Turns what the player typed, and the match they were in, into the report that gets posted.
/// </summary>
/// <remarks>
/// <para>
/// Everything that could identify a person is decided here, in one place, so that reviewing what a
/// report contains is reading one file. The build's version and platform go; the scenario, turn,
/// phase, AI policy and seat counts go. Player names, save names, file paths, the display name typed for
/// online play and the exception text of any crash do not — and the journal, which is the one part
/// large enough to hide something in, goes through <see cref="ReplayAnonymizer"/> first.
/// </para>
/// <para>
/// A journal that will not anonymize is dropped rather than sent as it is. The player ticked a box
/// that says anonymized, and the only honest reading of a failure to honour that is to send less.
/// </para>
/// </remarks>
public static class BugReportComposer
{
    /// <summary>
    /// The ceiling the server puts on the base64 journal, mirrored so an oversized one is dropped
    /// here rather than uploaded and refused.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>BUG_REPORT_LIMITS.stateBase64Bytes</c> in
    /// <c>multiplayer/packages/contracts/src/bug-reports.ts</c>, which is the source of truth. The
    /// contract generator emits records rather than constants, so this is the one number that has to
    /// be written on both sides; a mismatch costs an upload and a refusal, never a wrong report.
    /// </remarks>
    public const int MaximumStateBase64Bytes = 8 * 1024 * 1024;

    /// <summary>
    /// Lifts the live match into values nothing can change, on the thread that owns it.
    /// </summary>
    /// <remarks>
    /// This is the one step that cannot move off the update thread, and it is deliberately the
    /// cheap one: a serialize, no different from what saving a game already does, plus reading six
    /// numbers. Everything after it — replaying the match to anonymize it, then compressing hard —
    /// works from what comes out and can take as long as it likes with nothing shifting underneath.
    /// </remarks>
    public static CapturedJournal? Capture(
        MatchReplayRecorder? journal, BugReportMatchType matchType)
    {
        if (journal is null) return null;
        using var stream = new MemoryStream();
        MatchReplaySerializer.Save(stream, journal);
        return new CapturedJournal(
            stream.ToArray(), journal.State.Definitions, Describe(journal.State, matchType));
    }

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="message">What the player typed. Sent verbatim.</param>
    /// <param name="journal">
    /// The match as <see cref="Capture"/> lifted it, or null when there is no match. Its context is
    /// sent either way; its bytes only when <paramref name="includeState"/>.
    /// </param>
    /// <param name="includeState">Whether the player left the anonymized-state box ticked.</param>
    public static ComposedBugReport Compose(
        string message,
        CapturedJournal? journal,
        bool includeState)
    {
        ArgumentNullException.ThrowIfNull(message);
        var request = new SubmitBugReportRequest(
            message.Trim(),
            new BugReportBuild(BuildVersion(), Platform()),
            journal?.Context,
            null);
        if (!includeState || journal is null)
            return new ComposedBugReport(request, BugReportStateOutcome.NotRequested, 0);

        MatchReplayRecorder anonymized;
        try
        {
            using var source = new MemoryStream(journal.Json, writable: false);
            anonymized = ReplayAnonymizer.Anonymize(
                MatchReplaySerializer.TryLoadResumable(source, journal.Definitions)
                ?? throw new ReplayAnonymizationException(
                    "The captured journal is not in this build's replay format."));
        }
        catch (Exception exception) when (exception is ReplayAnonymizationException
                                          or InvalidDataException)
        {
            return new ComposedBugReport(request, BugReportStateOutcome.NotAnonymizable, 0);
        }

        byte[] archive;
        try
        {
            archive = ReplayArchive.Pack(anonymized, ReplayArchiveEffort.Smallest);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new ComposedBugReport(request, BugReportStateOutcome.TooLarge, 0);
        }

        var body = Convert.ToBase64String(archive);
        if (body.Length > MaximumStateBase64Bytes)
            return new ComposedBugReport(request, BugReportStateOutcome.TooLarge, archive.Length);

        var attached = request with
        {
            State = new BugReportState(
                BugReportCodec.Brotli,
                MatchReplaySerializer.CurrentFormatVersion,
                ReplayArchive.ReadHeader(archive).UncompressedBytes,
                ReplayArchive.Fingerprint(archive),
                Anonymized: true,
                body)
        };
        return new ComposedBugReport(attached, BugReportStateOutcome.Attached, archive.Length);
    }

    /// <summary>How the match a report was filed from was being played.</summary>
    public static BugReportMatchType MatchTypeOf(MatchState? state, bool online) => online
        ? BugReportMatchType.Online
        : state is not null && HumanSeats(state) > 1
            ? BugReportMatchType.Hotseat
            : BugReportMatchType.Single;

    /// <summary>
    /// The triage fields: where in the match the player was, as categories and counts.
    /// </summary>
    /// <remarks>
    /// Enough to sort a pile of reports without opening any of them, and not one field more. A
    /// scenario name and a phase name are the game's own vocabulary; a turn number and two seat
    /// counts are arithmetic.
    /// </remarks>
    private static BugReportContext Describe(MatchState state, BugReportMatchType matchType)
    {
        var humans = HumanSeats(state);
        return new BugReportContext(
            state.Setup.Scenario.ToString(),
            matchType,
            state.Coordinator.Turn,
            state.Coordinator.Phase.ToString(),
            humans,
            state.Setup.Players.Count - humans,
            state.Setup.AiPolicy.ToString());
    }

    private static int HumanSeats(MatchState state) =>
        state.Setup.Players.Count(player => player.Controller == PlayerController.Human);

    private static string BuildVersion() =>
        typeof(BugReportComposer).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// The platform family, and nothing beneath it.
    /// </summary>
    /// <remarks>
    /// <c>OSVersion.VersionString</c> would be more useful and is also, on some systems, a string
    /// with a machine name in it. The family is what a bug is ever actually reproduced against.
    /// </remarks>
    private static string Platform() => Environment.OSVersion.Platform.ToString();
}

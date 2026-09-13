using System.Net;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What a bug report carries, and what it deliberately does not.
/// </summary>
public sealed class BugReportComposerTests
{
    [Fact]
    public void AttachesAnAnonymizedReplayableJournalWhenTheBoxIsTicked()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET"));
        recorder.FinishUpkeep();

        var composed = BugReportComposer.Compose(
            "  Hire offers were empty.  ",
            BugReportComposer.Capture(recorder, BugReportMatchType.Single),
            includeState: true);

        Assert.Equal(BugReportStateOutcome.Attached, composed.StateOutcome);
        var state = Assert.IsType<BugReportState>(composed.Request.State);
        Assert.True(state.Anonymized);
        Assert.Equal(BugReportCodec.Brotli, state.Codec);
        Assert.Equal(MatchReplaySerializer.CurrentFormatVersion, state.ReplayFormatVersion);

        var archive = Convert.FromBase64String(state.Body);
        Assert.Equal(state.Sha256, ReplayArchive.Fingerprint(archive));
        Assert.Equal(state.UncompressedBytes, ReplayArchive.ReadHeader(archive).UncompressedBytes);
        var replayed = ReplayArchive.LoadAndReplay(archive, recorder.State.Definitions);
        Assert.Equal(["PLAYER 1", "PLAYER 2"], replayed.Setup.Players.Select(player => player.Name));
    }

    /// <summary>The message is sent as written, minus the whitespace around it.</summary>
    [Fact]
    public void SendsTheMessageVerbatim()
    {
        var composed = BugReportComposer.Compose(
            "  Two lines.\nThe second one.  ", journal: null, includeState: true);

        Assert.Equal("Two lines.\nThe second one.", composed.Request.Message);
    }

    [Fact]
    public void SendsNoJournalWhenTheBoxIsUnticked()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create());
        recorder.FinishUpkeep();

        var composed = BugReportComposer.Compose(
            "Something odd.",
            BugReportComposer.Capture(recorder, BugReportMatchType.Hotseat),
            includeState: false);

        Assert.Equal(BugReportStateOutcome.NotRequested, composed.StateOutcome);
        Assert.Null(composed.Request.State);
    }

    [Fact]
    public void SendsNoJournalWhenThereIsNoMatch()
    {
        var composed = BugReportComposer.Compose(
            "The title screen is blank.", journal: null, includeState: true);

        Assert.Equal(BugReportStateOutcome.NotRequested, composed.StateOutcome);
        Assert.Null(composed.Request.State);
        Assert.Null(composed.Request.Context);
    }

    /// <summary>
    /// The triage fields are categories and counts, and the build is a version and a platform.
    /// </summary>
    [Fact]
    public void DescribesWhereInTheMatchTheReportWasFiledAndNothingElse()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET", secondPlayerHuman: true));
        recorder.FinishUpkeep();

        var composed = BugReportComposer.Compose(
            "Odd.",
            BugReportComposer.Capture(recorder, BugReportMatchType.Hotseat),
            includeState: false);

        var context = Assert.IsType<BugReportContext>(composed.Request.Context);
        Assert.Equal("Greed", context.Scenario);
        Assert.Equal(BugReportMatchType.Hotseat, context.MatchType);
        Assert.Equal(recorder.State.Coordinator.Turn, context.Turn);
        Assert.Equal(recorder.State.Coordinator.Phase.ToString(), context.Phase);
        Assert.Equal(2, context.HumanPlayers);
        Assert.Equal(0, context.ComputerPlayers);
        Assert.NotEmpty(composed.Request.Client.Version);
        Assert.NotEmpty(composed.Request.Client.Platform);
    }

    [Theory]
    [InlineData(1, false, BugReportMatchType.Single)]
    [InlineData(2, false, BugReportMatchType.Hotseat)]
    [InlineData(2, true, BugReportMatchType.Online)]
    public void ReadsTheMatchKindFromTheRoster(
        int humans, bool online, BugReportMatchType expected)
    {
        var state = TestMatches.Create(secondPlayerHuman: humans > 1);

        Assert.Equal(expected, BugReportComposer.MatchTypeOf(state, online));
    }

    [Fact]
    public void ReadsTheMatchKindAsSingleWithNoMatchAtAll()
    {
        Assert.Equal(BugReportMatchType.Single, BugReportComposer.MatchTypeOf(null, online: false));
    }

    /// <summary>
    /// Nothing the background work reads can still be changing: the capture is bytes and values.
    /// </summary>
    /// <remarks>
    /// Anonymizing replays the match, so a capture that still pointed at the live recorder would be
    /// read while the player kept playing. This holds the seam: composing from a capture taken
    /// three turns ago describes and replays those three turns, not what the match has become.
    /// </remarks>
    [Fact]
    public void ComposesFromTheMatchAsItWasCapturedEvenAfterPlayContinues()
    {
        var recorder = new MatchReplayRecorder(TestMatches.Create("MARGARET"));
        recorder.FinishUpkeep();
        var captured = BugReportComposer.Capture(recorder, BugReportMatchType.Single);
        var turnAtCapture = recorder.State.Coordinator.Turn;
        var phaseAtCapture = recorder.State.Coordinator.Phase.ToString();

        // The match moves on while the report is being prepared, as it would on another thread.
        foreach (var player in recorder.State.Players) recorder.FinishCommand(player.Id);
        while (recorder.State.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        var composed = BugReportComposer.Compose("Odd.", captured, includeState: true);

        var context = Assert.IsType<BugReportContext>(composed.Request.Context);
        Assert.Equal(turnAtCapture, context.Turn);
        Assert.Equal(phaseAtCapture, context.Phase);
        Assert.NotEqual(recorder.State.Coordinator.Phase.ToString(), context.Phase);
        var state = Assert.IsType<BugReportState>(composed.Request.State);
        var replayed = ReplayArchive.LoadAndReplay(
            Convert.FromBase64String(state.Body), recorder.State.Definitions);
        Assert.Equal(phaseAtCapture, replayed.Coordinator.Phase.ToString());
    }
}

/// <summary>The free-form box: what it keeps, and how it is laid out.</summary>
public sealed class BugReportTextEditorTests
{
    [Fact]
    public void KeepsCaseNewlinesAndPunctuationExactlyAsTyped()
    {
        var editor = new BugReportTextEditor();
        foreach (var character in "Gang #3 vanished.\rIt was Hiding!") editor.Type(character);

        Assert.Equal("Gang #3 vanished.\nIt was Hiding!", editor.Value);
    }

    [Fact]
    public void BackspaceDeletesAndControlCharactersAreDropped()
    {
        var editor = new BugReportTextEditor();
        foreach (var character in "abc\t\0") editor.Type(character);
        editor.Type('\b');

        Assert.Equal("ab", editor.Value);
    }

    [Fact]
    public void StopsAtTheLengthTheServerAccepts()
    {
        var editor = new BugReportTextEditor();
        for (var index = 0; index < BugReportTextEditor.MaximumCharacters + 50; index++)
            editor.Type('x');

        Assert.True(editor.IsFull);
        Assert.Equal(BugReportTextEditor.MaximumCharacters, editor.Value.Length);
    }

    [Fact]
    public void WhitespaceOnlyCountsAsEmpty()
    {
        var editor = new BugReportTextEditor();
        foreach (var character in "  \n ") editor.Type(character);

        Assert.True(editor.IsEmpty);
    }

    [Fact]
    public void WrapsOnWordBoundariesAndHonoursTypedNewlines()
    {
        Assert.Equal(
            ["the quick", "brown fox", "jumps"],
            BugReportTextEditor.Wrap("the quick brown fox jumps", 10));
        Assert.Equal(["one", "two"], BugReportTextEditor.Wrap("one\ntwo", 10));
    }

    /// <summary>A pasted path is the case where losing the text would hurt most.</summary>
    [Fact]
    public void BreaksAWordLongerThanTheBoxRatherThanLosingIt()
    {
        Assert.Equal(["abcde", "fghij", "k"], BugReportTextEditor.Wrap("abcdefghijk", 5));
    }

    /// <summary>Somebody typing is looking at the end of what they wrote, not the beginning.</summary>
    [Fact]
    public void ShowsTheTailWithACaretWhenFocused()
    {
        var editor = new BugReportTextEditor();
        foreach (var character in "one\ntwo\nthree") editor.Type(character);

        Assert.Equal(["two", "three_"], editor.DisplayLines(10, 2, focused: true));
        Assert.Equal(["two", "three"], editor.DisplayLines(10, 2, focused: false));
    }
}

/// <summary>The one route that needs no token, and the failures a player can be told about.</summary>
public sealed class BugReportSubmitterTests
{
    private static SubmitBugReportRequest Report() => new(
        "It broke.", new BugReportBuild("0.0.1", "Unix"), null, null);

    [Fact]
    public async Task PostsToTheBugReportRouteAndReadsTheReceipt()
    {
        using var server = new FakeMultiplayerServer();
        using var http = BugReportSubmitter.CreateHttpClient(server);
        server.Answer(
            HttpMethod.Post,
            "/bug-reports",
            new BugReportReceipt(
                "abc", "2026-01-01T00:00:00.000Z", BugReportReceiptStateStored.Stored),
            HttpStatusCode.Created);

        var receipt = await new BugReportSubmitter(http, new Uri("http://localhost:8787"))
            .SubmitAsync(Report(), CancellationToken.None);

        Assert.Equal("abc", receipt.Id);
        Assert.Equal(BugReportReceiptStateStored.Stored, receipt.StateStored);
        var request = Assert.Single(server.Requests);
        Assert.Equal("/api/v1/bug-reports", request.Path);
        Assert.Contains("It broke.", request.Body, StringComparison.Ordinal);
    }

    /// <summary>A report is not a match, so nothing about it is authenticated.</summary>
    [Fact]
    public async Task SendsNoCredential()
    {
        using var server = new FakeMultiplayerServer();
        using var http = BugReportSubmitter.CreateHttpClient(server);
        server.Answer(
            HttpMethod.Post,
            "/bug-reports",
            new BugReportReceipt(
                "abc", "2026-01-01T00:00:00.000Z", BugReportReceiptStateStored.NotSent),
            HttpStatusCode.Created);

        await new BugReportSubmitter(http, new Uri("http://localhost:8787"))
            .SubmitAsync(Report(), CancellationToken.None);

        Assert.DoesNotContain(
            "cop_", Assert.Single(server.Requests).Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, BugReportFailure.NotAccepted)]
    [InlineData(HttpStatusCode.TooManyRequests, BugReportFailure.TooMany)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, BugReportFailure.Refused)]
    [InlineData(HttpStatusCode.UnprocessableEntity, BugReportFailure.Refused)]
    [InlineData(HttpStatusCode.InternalServerError, BugReportFailure.ServerError)]
    public async Task NamesEachRefusalTheServerCanGive(
        HttpStatusCode status, BugReportFailure expected)
    {
        using var server = new FakeMultiplayerServer();
        using var http = BugReportSubmitter.CreateHttpClient(server);
        server.Answer(HttpMethod.Post, "/bug-reports", null, status);

        var failure = await Assert.ThrowsAsync<BugReportException>(
            () => new BugReportSubmitter(http, new Uri("http://localhost:8787"))
                .SubmitAsync(Report(), CancellationToken.None));

        Assert.Equal(expected, failure.Failure);
    }

    /// <summary>A server published under a path keeps it, exactly as the match client does.</summary>
    [Theory]
    [InlineData("http://host", "/api/v1/bug-reports")]
    [InlineData("http://host/", "/api/v1/bug-reports")]
    [InlineData("http://host/game", "/game/api/v1/bug-reports")]
    public async Task BuildsTheRouteUnderThePathTheServerIsPublishedAt(
        string origin, string expected)
    {
        using var server = new FakeMultiplayerServer();
        using var http = BugReportSubmitter.CreateHttpClient(server);
        server.Answer(
            HttpMethod.Post,
            "/bug-reports",
            new BugReportReceipt(
                "abc", "2026-01-01T00:00:00.000Z", BugReportReceiptStateStored.NotSent),
            HttpStatusCode.Created);

        await new BugReportSubmitter(http, new Uri(origin))
            .SubmitAsync(Report(), CancellationToken.None);

        Assert.Equal(expected, Assert.Single(server.Requests).Path);
    }

    /// <summary>
    /// The client this class builds carries no deadline of its own, so its caller's is the one that
    /// applies.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpClient.Timeout"/> defaults to a hundred seconds and is applied on top of any
    /// token passed to a send, so the shorter of the two wins. A default-constructed client would
    /// quietly overrule <see cref="BugReportSubmitter.DefaultTimeout"/> and abandon a megabyte
    /// upload on a slow uplink at a hundred seconds — the case those three minutes exist for.
    /// </remarks>
    [Fact]
    public void BuildsAClientThatDoesNotOverruleTheReportDeadline()
    {
        using var client = BugReportSubmitter.CreateHttpClient();
        using var untouched = new HttpClient();

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        Assert.True(untouched.Timeout < BugReportSubmitter.DefaultTimeout,
            "the default client's own timeout is what this factory exists to remove");
    }

    /// <summary>
    /// A client with a shorter clock is refused where the mistake is, not a hundred seconds into
    /// somebody's report — where it would be indistinguishable from an unreachable server.
    /// </summary>
    [Fact]
    public void RefusesAClientWhoseOwnTimeoutWouldWin()
    {
        using var server = new FakeMultiplayerServer();
        using var impatient = new HttpClient(server) { Timeout = TimeSpan.FromSeconds(5) };

        var refusal = Assert.Throws<ArgumentException>(() => new BugReportSubmitter(impatient));

        Assert.Equal("http", refusal.ParamName);
        Assert.Contains(nameof(BugReportSubmitter.CreateHttpClient), refusal.Message,
            StringComparison.Ordinal);
    }

    /// <summary>A caller that shortens the deadline itself is taken at its word.</summary>
    [Fact]
    public void AcceptsAClientThatIsPatientEnoughForTheDeadlineItIsGiven()
    {
        using var server = new FakeMultiplayerServer();
        using var client = new HttpClient(server) { Timeout = TimeSpan.FromSeconds(30) };

        var submitter = new BugReportSubmitter(client, timeout: TimeSpan.FromSeconds(10));

        Assert.NotNull(submitter);
    }

    /// <summary>The placeholder until the public server exists; changing it is one line.</summary>
    [Fact]
    public void DefaultsToTheCentralServerRatherThanWhicheverLobbyIsOpen()
    {
        Assert.Equal(new Uri("http://localhost:8787"), BugReportEndpoint.Default);
    }
}

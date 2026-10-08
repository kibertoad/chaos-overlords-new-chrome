using System.Net;
using System.Text.RegularExpressions;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Resolution;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A match played from views (docs/MULTIPLAYER.md, "Planning on a view"): the server resolves every
/// turn and serves each seat its view, and the client plans on that and resolves nothing.
/// </summary>
/// <remarks>
/// The server's half is scripted with <see cref="AuthoritativeMatch"/>, the class the server's
/// resolver plays the match with, so each view served is the one the server would serve.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    private static readonly OriginalData ViewDefinitions = BundledOriginalData.Load();

    private const string At = "2026-09-10T12:00:00.000Z";

    /// <summary>The match view of a match played from views: no seed while it runs.</summary>
    private static MatchView ViewMatch() => View() with { Seed = null, Refereed = true, SeatViews = true };

    private static (MultiplayerMatchSession Session, FakeMultiplayerServer Server, HttpClient Http)
        RunningFromViews(AuthoritativeMatch match, Action<FakeMultiplayerServer>? configure = null) =>
        Running(
            matchView: ViewMatch(),
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/view", ServedView(match, slot: 0));
                fake.Answer(HttpMethod.Get, "/events", new EventPage([]));
                fake.Answer(
                    HttpMethod.Get, "/orders/mine", new OwnSubmissionView(1, null, Ready: false, null));
                configure?.Invoke(fake);
            });

    private static AuthoritativeMatch ServerMatch() =>
        AuthoritativeMatch.Bootstrap(ViewDefinitions, Seed, GameSettings, Roster);

    /// <summary>A seat's view of the match as the server serves it: the archive of its save payload.</summary>
    private static ServedSeatView ServedView(AuthoritativeMatch match, int slot)
    {
        var payload = match.SeatViewPayload(slot)
            ?? throw new InvalidOperationException($"seat {slot} has no view");
        using var stream = new MemoryStream(payload, writable: false);
        var view = SeatView.Load(stream, ViewDefinitions, new PlayerId(slot));
        return new ServedSeatView(
            match.Turn,
            slot,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerSessionVersion.Current,
            MatchStateClone.ToBase64(view));
    }

    private static SnapshotView ServerSnapshot(AuthoritativeMatch match) => new(
        match.Turn - 1,
        NativeSaveSerializer.CurrentFormatVersion,
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        match.StateHash,
        "server",
        At,
        match.Snapshot());

    /// <summary>
    /// Seals the match's open turn on the server with nobody ordering anything, and writes the
    /// events a view match publishes for it, the confirmation last.
    /// </summary>
    /// <returns>The sequence of the last event written.</returns>
    private static int SealAndConfirm(
        AuthoritativeMatch match,
        FakeMultiplayerServer server,
        int seq,
        Action? beforeConfirmation = null)
    {
        var turn = match.Turn;
        var sealedOrders = SealedOrders(turn);
        server.Answer(HttpMethod.Get, $"/turns/{turn}/orders", sealedOrders);
        server.Events.Write(SealedFrame(++seq, turn));
        match.Apply(new TurnSealedEvent(seq, MatchId, At, new(turn, sealedOrders.OrderSetHash)), sealedOrders);
        if (!match.IsFinished)
            server.Events.Write(Frame(++seq, "turn.opened", $$"""{"turn":{{turn + 1}},"deadlineAt":null}"""));
        beforeConfirmation?.Invoke();
        server.Events.Write(Frame(
            ++seq, "turn.confirmed", $$"""{"turn":{{turn}},"stateHash":"{{match.StateHash}}"}"""));
        return seq;
    }

    private static bool ReadsASealedSet(FakeMultiplayerServer server) => server.Requests.Any(
        request => request.Method == HttpMethod.Get
            && Regex.IsMatch(request.Path, "/turns/[0-9]+/orders$"));

    [Fact]
    public async Task AMatchPlayedFromViewsIsPlayedToItsEndWithoutResolvingAnythingOnTheClient()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match);
        using var _ = http;
        await using var __ = session;

        Assert.True(session.PlaysFromViews);
        Assert.Null(session.Bootstrap);
        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);
        Assert.Equal(new PlayerId(0), resumed.State.ViewedBy);
        Assert.Equal(1, resumed.Turn!.State.Coordinator.Turn);

        var seq = 7;
        var turns = 0;
        while (!match.IsFinished)
        {
            var turn = match.Turn;
            seq = SealAndConfirm(match, server, seq, () =>
            {
                if (match.IsFinished)
                {
                    server.Answer(HttpMethod.Get, "/view", Envelope("match_finished"), HttpStatusCode.Conflict);
                    server.Answer(HttpMethod.Get, "/snapshots/latest", ServerSnapshot(match));
                }
                else
                {
                    server.Answer(HttpMethod.Get, "/view", ServedView(match, slot: 0));
                }
            });
            var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);
            turns++;
            Assert.Equal(turn, resolved.Turn);
            Assert.Equal(match.StateHash, resolved.StateHash);
            if (match.IsFinished)
            {
                // The whole final state, for the endgame screen.
                Assert.Null(resolved.Planning);
                Assert.Null(resolved.State.ViewedBy);
                Assert.NotNull(resolved.State.Outcome);
                Assert.Equal(match.StateHash, MatchStateHasher.ComputeFingerprint(resolved.State));
            }
            else
            {
                Assert.Equal(new PlayerId(0), resolved.State.ViewedBy);
                Assert.Equal(turn + 1, resolved.Planning!.State.Coordinator.Turn);
                Assert.Equal(new PlayerId(0), resolved.Planning.State.Coordinator.ActivePlayer);
                // What the client holds cannot resolve, so nothing on it could have.
                Assert.Throws<InvalidOperationException>(() => resolved.State.FinishCommand(new PlayerId(0)));
            }
        }
        server.Events.Write(Frame(++seq, "match.statusChanged", """{"status":"finished"}"""));
        await WaitFor<MultiplayerNotice.MatchFinished>(session);

        Assert.True(turns > 1);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/report"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/snapshots"));
        Assert.False(ReadsASealedSet(server));
    }

    [Fact]
    public async Task AViewTheServerHasNotPreparedIsAskedForAgain()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match, fake => fake.AnswerOnce(
            HttpMethod.Get, "/view", Envelope("view_not_ready"), HttpStatusCode.Conflict));
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(1, resumed.State.Coordinator.Turn);
        Assert.Equal(2, server.CallsTo(HttpMethod.Get, "/view"));
    }

    [Fact]
    public async Task AResumedViewCarriesTheDraftTheServerHolds()
    {
        var match = ServerMatch();
        var view = ServedView(match, slot: 0);
        var state = MatchStateClone.ViewFromBase64(view.Body, ViewDefinitions, new PlayerId(0));
        var offer = state.Players[0].HireOfferSlots
            .Select(slot => slot.GangDefinitionId)
            .First(id => id is not null)!.Value;
        var draft = SpeculativeTurn.For(state, ViewDefinitions, 0);
        Assert.True(draft.SnubHireOffer(offer).Accepted);
        var document = draft.Build();
        var (session, server, http) = RunningFromViews(match, fake => fake.Answer(
            HttpMethod.Get,
            "/orders/mine",
            new OwnSubmissionView(1, document, Ready: false, OrderDigest.OfDocument(document))));
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        // The planning copy is the view with the draft replayed on it, so it can be edited further.
        Assert.Equal(OrderDigest.OfDocument(document), OrderDigest.OfDocument(resumed.Turn!.Build()));
        Assert.Equal(offer, resumed.Turn.State.Players[0].SnubbedHireOffer);
    }

    [Fact]
    public async Task AnEliminatedSeatPassesEachTurnUntilTheMatchEnds()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match);
        using var _ = http;
        await using var __ = session;
        await WaitFor<MultiplayerNotice.Resumed>(session);

        var seq = SealAndConfirm(match, server, 7, () => server.Answer(
            HttpMethod.Get, "/view", Envelope("seat_out"), HttpStatusCode.Conflict));
        var seatOut = await WaitFor<MultiplayerNotice.SeatOut>(session);
        Assert.Equal(2, seatOut.Turn);
        await Until(() => server.CallsTo(HttpMethod.Put, "/turns/2/orders") >= 1, "turn 2 was passed");

        server.Events.Write(SealedFrame(++seq, 2));
        server.Events.Write(Frame(++seq, "turn.opened", """{"turn":3,"deadlineAt":null}"""));
        await Until(() => server.CallsTo(HttpMethod.Put, "/turns/3/orders") >= 1, "turn 3 was passed");

        Assert.Contains("\"ready\":true", server.BodiesSentTo(HttpMethod.Put, "/turns/3/orders")[0]);
        Assert.Equal("[]", Regex.Match(
            server.BodiesSentTo(HttpMethod.Put, "/turns/3/orders")[0], "\"ops\":(\\[[^\\]]*\\])").Groups[1].Value);
    }

    /// <summary>
    /// A report filed during a match played from views carries what the client has: its view and
    /// the turn being planned on it. That replays as the view, and refuses to replay as a match.
    /// </summary>
    [Fact]
    public void AReportFromAViewMatchCarriesTheViewAndRefusesReplayAsAMatch()
    {
        var match = ServerMatch();
        var view = MatchStateClone.ViewFromBase64(
            ServedView(match, slot: 0).Body, ViewDefinitions, new PlayerId(0));
        var offer = view.Players[0].HireOfferSlots
            .Select(slot => slot.GangDefinitionId)
            .First(id => id is not null)!.Value;
        var planning = SpeculativeTurn.For(view, ViewDefinitions, 0);
        Assert.True(planning.SnubHireOffer(offer).Accepted);

        var composed = BugReportComposer.Compose(
            "The hire dock is wrong.",
            BugReportComposer.Capture(planning.Replay, BugReportMatchType.Online),
            includeState: true);

        Assert.Equal(BugReportStateOutcome.Attached, composed.StateOutcome);
        var archive = Convert.FromBase64String(composed.Request.State!.Body);
        var refused = Assert.Throws<InvalidDataException>(
            () => ReplayArchive.LoadAndReplay(archive, ViewDefinitions));
        Assert.Equal(0, SeatViewJournal.SeatOf(refused));
        Assert.Contains("cannot be replayed as a match", refused.Message, StringComparison.Ordinal);

        using var json = new MemoryStream(ReplayArchive.Unpack(archive), writable: false);
        var replayed = MatchReplaySerializer.TryLoadResumable(json, ViewDefinitions)!.State;
        Assert.Equal(new PlayerId(0), replayed.ViewedBy);
        Assert.Equal(offer, replayed.Players[0].SnubbedHireOffer);
        Assert.Equal(ReplayAnonymizer.SeatName(0), replayed.Setup.Players[0].Name);
        Assert.Throws<InvalidOperationException>(() => replayed.FinishCommand(new PlayerId(0)));
    }

    /// <summary>
    /// Once a match played from views has ended, the session rebuilds the whole match from the seed
    /// and the sealed sets the server releases, and a report filed then replays as a match.
    /// </summary>
    [Fact]
    public async Task AReportFiledAfterAViewMatchEndsReplaysTheWholeMatch()
    {
        var match = ServerMatch();
        var seq = 1;
        var history = new List<MatchEvent>
        {
            new MatchStartedEvent(seq, MatchId, At, new MatchStartedEventPayload(null, Roster)),
        };
        var sealedSets = new Dictionary<int, SealedOrdersView>();
        while (!match.IsFinished)
        {
            var turn = match.Turn;
            var sealedOrders = SealedOrders(turn);
            sealedSets[turn] = sealedOrders;
            var sealedTurn = new TurnSealedEvent(++seq, MatchId, At, new(turn, sealedOrders.OrderSetHash));
            history.Add(sealedTurn);
            match.Apply(sealedTurn, sealedOrders);
            if (!match.IsFinished)
                history.Add(new TurnOpenedEvent(++seq, MatchId, At, new(turn + 1, null)));
            history.Add(new TurnConfirmedEvent(++seq, MatchId, At, new(turn, match.StateHash)));
        }
        var finished = ViewMatch() with
        {
            Status = MatchStatus.Finished,
            Seed = Seed,
            CurrentTurn = match.Turn - 1,
            LastEventSeq = seq,
        };
        // The session starts from the running match and finds it finished when it restores.
        var (session, server, http) = Running(
            matchView: ViewMatch(),
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, $"/matches/{MatchId}", new MatchDetail(finished, "CODE1234", "p1"));
                // The resume reads the log from where the session left it, the rebuild from the start.
                fake.AnswerOnce(
                    HttpMethod.Get, "/events", new EventPage(history.Where(@event => @event.Seq > 7).ToArray()));
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                foreach (var (turn, sealedOrders) in sealedSets)
                    fake.Answer(HttpMethod.Get, $"/turns/{turn}/orders", sealedOrders);
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);
        Assert.NotNull(resumed.State.Outcome);
        Assert.Equal(match.StateHash, MatchStateHasher.ComputeFingerprint(resumed.State));
        await Until(() => session.ReleasedJournal is not null, "the whole match's journal was rebuilt");

        var journal = session.ReleasedJournal!;
        using (var whole = new MemoryStream())
        {
            MatchReplaySerializer.Save(whole, journal);
            whole.Position = 0;
            var replayed = MatchReplaySerializer.LoadAndReplay(whole, ViewDefinitions);
            Assert.Null(replayed.ViewedBy);
            Assert.Equal(match.StateHash, MatchStateHasher.ComputeFingerprint(replayed));
        }

        var composed = BugReportComposer.Compose(
            "The awards are wrong.",
            BugReportComposer.Capture(journal, BugReportMatchType.Online),
            includeState: true);
        Assert.Equal(BugReportStateOutcome.Attached, composed.StateOutcome);
        var report = ReplayArchive.LoadAndReplay(
            Convert.FromBase64String(composed.Request.State!.Body), ViewDefinitions);
        Assert.NotNull(report.Outcome);
        Assert.Equal(resumed.State.Coordinator.Turn, report.Coordinator.Turn);
    }

    /// <summary>
    /// A seat already out still passes the open turn when a reconnect is what finds it, since the
    /// <c>turn.opened</c> that would have passed it is in the history the restore skips over.
    /// </summary>
    [Fact]
    public async Task AReconnectOfASeatAlreadyOutPassesTheOpenTurn()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match);
        using var _ = http;
        await using var __ = session;
        await WaitFor<MultiplayerNotice.Resumed>(session);
        var seq = SealAndConfirm(match, server, 7, () => server.Answer(
            HttpMethod.Get, "/view", Envelope("seat_out"), HttpStatusCode.Conflict));
        await WaitFor<MultiplayerNotice.SeatOut>(session);
        await Until(() => server.CallsTo(HttpMethod.Put, "/turns/2/orders") >= 1, "turn 2 was passed");

        // Turn 2 sealed and turn 3 opened while the stream was away.
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(ViewMatch() with { CurrentTurn = 3, LastEventSeq = seq + 2 }, "CODE1234", "p1"));
        session.RequestResync();

        await Until(() => server.CallsTo(HttpMethod.Put, "/turns/3/orders") >= 1, "the restore passed turn 3");
    }

    /// <summary>
    /// A reconnect after the other players handed this seat to the computer goes on watching the
    /// match, as lockstep does, rather than failing over the view a computer seat is not served.
    /// </summary>
    [Fact]
    public async Task AReconnectAfterTheSeatWasHandedToTheComputerKeepsWatching()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match);
        using var _ = http;
        await using var __ = session;
        await WaitFor<MultiplayerNotice.Resumed>(session);
        server.Answer(HttpMethod.Get, "/view", Envelope("not_active"), HttpStatusCode.Forbidden);
        server.Answer(
            HttpMethod.Get,
            $"/matches/{MatchId}",
            new MatchDetail(ViewMatch() with { LastEventSeq = 8 }, "CODE1234", "p1"));
        server.Events.Write(Frame(8, "match.playerTakenOver", """{"playerId":"p1"}"""));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.TakeoverVoteClosed>(session, seen);

        session.RequestResync();

        // The stream is opened again only by a restore that kept the session.
        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") >= 2, "the stream was reopened");
        while (session.TryDequeueNotice(out var notice)) seen.Add(notice);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// A pump that is behind hands a view over at the confirmation it belongs to, with that turn's
    /// hash, and not at the earlier confirmation it happened to be reading.
    /// </summary>
    [Fact]
    public async Task AViewServedAheadOfTheConfirmationBeingReadWaitsForItsOwn()
    {
        var match = ServerMatch();
        var (session, server, http) = RunningFromViews(match);
        using var _ = http;
        await using var __ = session;
        await WaitFor<MultiplayerNotice.Resumed>(session);

        // Two turns resolve on the server before the client reads either confirmation.
        using var elsewhere = new FakeMultiplayerServer();
        var seq = SealAndConfirm(match, elsewhere, 7);
        var firstHash = match.StateHash;
        SealAndConfirm(match, elsewhere, seq);
        var secondHash = match.StateHash;
        server.Answer(HttpMethod.Get, "/view", ServedView(match, slot: 0));
        server.Events.Write(SealedFrame(8, 1));
        server.Events.Write(Frame(9, "turn.opened", """{"turn":2,"deadlineAt":null}"""));
        server.Events.Write(Frame(10, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{firstHash}}"}"""));
        server.Events.Write(SealedFrame(11, 2));
        server.Events.Write(Frame(12, "turn.opened", """{"turn":3,"deadlineAt":null}"""));
        server.Events.Write(Frame(13, "turn.confirmed", $$"""{"turn":2,"stateHash":"{{secondHash}}"}"""));

        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(2, resolved.Turn);
        Assert.Equal(secondHash, resolved.StateHash);
        Assert.Equal(3, resolved.Planning!.State.Coordinator.Turn);
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
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
}

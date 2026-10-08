// Writes a transcript of one online match for tools/ResolverDeterminism/check.mjs to replay through
// the WebAssembly resolver (src/Rechaos.Resolver.Wasm).
//
//   dotnet run --project tools/ResolverDeterminism -c Release -- <transcript.json> [seed] [turns] [duration]
//
// The match is played twice natively while the transcript is written: once the way a client plays
// it (bootstrap, CommandPhase, SealedTurnApplier, SeatControl) and once through AuthoritativeMatch,
// the resolver's C# entry point, fed the match's event log as the server stores it. The two must
// agree on every hash, and the hashes they agree on are what the WebAssembly build is held to. The
// two human seats order what the computer would order for them, so the sealed sets carry real
// commands, hires and refusals rather than empty documents; slot 1 is handed to the computer before
// turn 4 and back before turn 7. The log also carries the events that change nothing (turn.opened,
// turn.readiness), as the server's does.
//
// At the end, a match picked up from the snapshot after turn 10 folds the whole log from its start,
// as a reconnecting client does (MatchHistory), and must reach the final hash too. The snapshot step
// also carries each human seat's view of the turn after it (SeatView), which a host must serve byte
// for byte.
using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Resolution;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: ResolverDeterminism <transcript.json> [seed] [turns] [duration]");
    Console.Error.WriteLine("       ResolverDeterminism --read-archive <archive.txt> <stateHash>");
    return 2;
}
if (args[0] == "--read-archive")
{
    // A snapshot archive a resolver host wrote, read the way every client reads one from the server.
    if (args.Length < 3) return 2;
    AuthoritativeMatch.FromSnapshot(BundledOriginalData.Load(), File.ReadAllText(args[1]).Trim(), args[2], []);
    Console.WriteLine($"the archive restores to {args[2]}");
    return 0;
}
var output = args[0];
var seed = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1996;
var turns = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 40;
// SixMonths by default; a longer duration (OneYear, TwoYears, FourYears) makes a transcript for
// measuring how the resolver's memory grows over a long match.
var duration = args.Length > 3 ? Enum.Parse<GameDuration>(args[3]) : GameDuration.SixMonths;

var definitions = BundledOriginalData.Load();
var settings = new MultiplayerGameSettings(
    ScenarioId.Greed, duration, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
PlayerView[] players =
[
    new("p1", 0, "ADA", PortraitId: 6, Status: WirePlayerStatus.Active, IsHost: true),
    new("p2", 1, "GRACE", PortraitId: 7, Status: WirePlayerStatus.Active, IsHost: false),
];
var handOvers = new Dictionary<int, PlayerController>
{
    [4] = PlayerController.Computer,
    [7] = PlayerController.Human,
};
const string MatchId = "transcript";
var log = new List<(MatchEvent Event, SealedOrdersView? Set)>();
MatchEvent Logged(MatchEvent @event, SealedOrdersView? set = null)
{
    log.Add((@event, set));
    return @event;
}

var client = new MatchReplayRecorder(MatchBootstrapFactory.Create(definitions, seed, settings, players));
CommandPhase.Enter(client);
var resolver = AuthoritativeMatch.Bootstrap(definitions, seed, settings, players);
var bootstrapHash = Require(Hash(client.State), resolver.StateHash, "the bootstrap");

var steps = new JsonArray();
void Step(MatchEvent @event, SealedOrdersView? set, string what)
{
    var hash = Require(Hash(client.State), resolver.Apply(Logged(@event, set), set), what);
    var step = new JsonObject
    {
        ["kind"] = "event",
        ["event"] = JsonNode.Parse(WireJson.Write<MatchEvent>(@event)),
        ["hash"] = hash,
    };
    if (set is not null) step["sealedOrders"] = JsonNode.Parse(WireJson.Write(set));
    steps.Add(step);
}
string? checkpoint = null;
string? checkpointHash = null;
while (client.State.Outcome is null && client.State.Coordinator.Turn <= turns)
{
    var turn = client.State.Coordinator.Turn;
    var seq = log.Count + 1;
    if (turn > 1) Step(new TurnOpenedEvent(seq++, MatchId, "t", new(turn, null)), null, $"turn {turn} opening");
    if (handOvers.TryGetValue(turn, out var controller))
    {
        SeatControl.HandOver(client, 1, controller);
        MatchEvent handOver = controller == PlayerController.Computer
            ? new MatchPlayerTakenOverEvent(seq++, MatchId, "t", new("p2"))
            : new MatchPlayerReturnedEvent(seq++, MatchId, "t", new("p2", ReplacedComputer: true));
        Step(handOver, null, $"the handover before turn {turn}");
    }
    Step(new TurnReadinessEvent(seq++, MatchId, "t", new(turn, "p1", true)), null, $"readiness on turn {turn}");

    var sealedOrders = Seal(turn, client.State, players, definitions);
    SealedTurnApplier.Apply(client, sealedOrders);
    Step(
        new TurnSealedEvent(seq, MatchId, "t", new(turn, sealedOrders.OrderSetHash)),
        sealedOrders,
        $"turn {turn}");
    if (turn == 10)
    {
        // A resolver that lost its runtime picks the match up from its own snapshot.
        checkpoint = resolver.Snapshot();
        checkpointHash = resolver.StateHash;
        // Each human seat's view of turn 11, as the core projects it from the client's state: what a
        // server playing the match from views serves that seat. A seat that is out, or any seat of a
        // finished match, has no view (AuthoritativeMatch.SeatViewPayload), and is written as null.
        var seatViews = new JsonObject();
        foreach (var player in players)
        {
            if (client.State.Outcome is not null
                || client.State.FindPlayer(new PlayerId(player.Slot)) is not { } seat
                || seat.Status != Rechaos.Core.GameModel.PlayerStatus.Active)
            {
                seatViews[player.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture)] = null;
                continue;
            }
            using var view = new MemoryStream();
            SeatView.Save(view, SeatView.Project(client.State, new PlayerId(player.Slot)));
            seatViews[player.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                Convert.ToBase64String(view.ToArray());
        }
        steps.Add(new JsonObject
        {
            ["kind"] = "snapshot",
            ["savePayload"] = Convert.ToBase64String(resolver.SavePayload()),
            ["archive"] = checkpoint,
            ["hash"] = checkpointHash,
            ["seatViews"] = seatViews,
        });
    }
}

if (checkpoint is not null && checkpointHash is not null)
{
    var walked = AuthoritativeMatch.FromSnapshot(definitions, checkpoint, checkpointHash, players, logTurn: 1);
    foreach (var (@event, set) in log) walked.Apply(@event, set);
    Require(Hash(client.State), walked.StateHash, "the whole log folded over the snapshot");
}

var transcript = new JsonObject
{
    ["sessionVersion"] = AuthoritativeMatch.SessionVersion,
    ["seed"] = seed,
    ["gameSettings"] = JsonNode.Parse(JsonSerializer.Serialize(settings.ToWire(), WireJson.Options)),
    ["players"] = JsonNode.Parse(WireJson.Write(players)),
    ["bootstrapHash"] = bootstrapHash,
    ["steps"] = steps,
    ["finished"] = client.State.Outcome is not null,
};
File.WriteAllText(output, transcript.ToJsonString());
Console.WriteLine(
    $"wrote {steps.Count} steps through turn {client.State.Coordinator.Turn - 1}, final hash {Hash(client.State)}");
return 0;

static string Hash(MatchState state) => MatchStateHasher.ComputeFingerprint(state);

static string Require(string expected, string actual, string what)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"the resolver disagrees with the client after {what}");
    return expected;
}

// One document per human-controlled seat, holding what the computer would order for it, planned
// on a copy so the planning itself leaves no trace in the match.
static SealedOrdersView Seal(int turn, MatchState state, PlayerView[] players, OriginalData definitions)
{
    var entries = new List<SealedPlayerOrders>();
    foreach (var player in players)
    {
        var id = new PlayerId(player.Slot);
        if (state.FindPlayer(id) is not { } seat
            || seat.Setup.Controller != PlayerController.Human
            || seat.Status != Rechaos.Core.GameModel.PlayerStatus.Active)
            continue;
        // The planner plans only for a computer seat, so the copy hands this one over first
        var copy = new MatchReplayRecorder(MatchStateClone.Of(state, definitions));
        SeatControl.HandOver(copy, player.Slot, PlayerController.Computer);
        // and, as SpeculativeTurn does for the interface, brings the Command phase round to it.
        while (copy.State.Coordinator.ActivePlayer is { } active && active != id) copy.FinishCommand(active);
        copy.PrepareAiPlanning(id);
        var builder = new OrderDocumentBuilder(id);
        foreach (var command in AiPolicyPlanner.Plan(copy.State, id)) builder.Submit(command);
        var hiring = copy.PrepareAiHiring(id);
        if (hiring.Choice is { } choice) builder.QueueHire(id, choice.GangDefinitionId, choice.SectorId);
        else if (hiring.RejectedGangDefinitionId is { } rejected) builder.SnubHireOffer(id, rejected);
        var document = builder.Build();
        entries.Add(new SealedPlayerOrders(player.Id, player.Slot, document, OrderDigest.OfDocument(document)));
    }
    return new SealedOrdersView(turn, OrderDigest.OfSet(entries), entries);
}

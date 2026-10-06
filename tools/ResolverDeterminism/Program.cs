// Writes a transcript of one online match for tools/ResolverDeterminism/check.mjs to replay through
// the WebAssembly resolver (src/Rechaos.Resolver.Wasm).
//
//   dotnet run --project tools/ResolverDeterminism -c Release -- <transcript.json> [seed] [turns]
//
// The match is played twice natively while the transcript is written: once the way a client plays
// it (bootstrap, CommandPhase, SealedTurnApplier, SeatControl) and once through AuthoritativeMatch,
// the resolver's C# entry point. The two must agree on every hash, and the hashes they agree on are
// what the WebAssembly build is held to. The two human seats order what the computer would order
// for them, so the sealed sets carry real commands, hires and refusals rather than empty documents;
// slot 1 is handed to the computer before turn 4 and back before turn 7.
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
    Console.Error.WriteLine("usage: ResolverDeterminism <transcript.json> [seed] [turns]");
    return 2;
}
var output = args[0];
var seed = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1996;
var turns = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 40;

var definitions = BundledOriginalData.Load();
var settings = new MultiplayerGameSettings(
    ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
PlayerView[] players =
[
    new("p1", 0, "ADA", PortraitId: 6, Status: WirePlayerStatus.Active, IsHost: true),
    new("p2", 1, "GRACE", PortraitId: 7, Status: WirePlayerStatus.Active, IsHost: false),
];
var handOvers = new Dictionary<int, (int Slot, PlayerController Controller)>
{
    [4] = (1, PlayerController.Computer),
    [7] = (1, PlayerController.Human),
};

var client = new MatchReplayRecorder(MatchBootstrapFactory.Create(definitions, seed, settings, players));
CommandPhase.Enter(client);
var resolver = AuthoritativeMatch.Bootstrap(definitions, seed, settings, players);
var bootstrapHash = Require(Hash(client.State), resolver.StateHash, "the bootstrap");

var steps = new JsonArray();
while (client.State.Outcome is null && client.State.Coordinator.Turn <= turns)
{
    var turn = client.State.Coordinator.Turn;
    if (handOvers.TryGetValue(turn, out var handOver))
    {
        SeatControl.HandOver(client, handOver.Slot, handOver.Controller);
        resolver.HandOverSeat(handOver.Slot, handOver.Controller);
        var hash = Require(Hash(client.State), resolver.StateHash, $"the handover before turn {turn}");
        steps.Add(new JsonObject
        {
            ["kind"] = "handOver",
            ["slot"] = handOver.Slot,
            ["toComputer"] = handOver.Controller == PlayerController.Computer,
            ["hash"] = hash,
        });
    }

    var sealedOrders = Seal(turn, client.State, players, definitions);
    var clientHash = SealedTurnApplier.Apply(client, sealedOrders);
    var resolverHash = resolver.ApplySealedTurn(sealedOrders);
    Require(clientHash, resolverHash, $"turn {turn}");
    steps.Add(new JsonObject
    {
        ["kind"] = "sealed",
        ["sealedOrders"] = JsonNode.Parse(WireJson.Write(sealedOrders)),
        ["hash"] = clientHash,
    });
    if (turn == 10)
    {
        // A resolver that lost its runtime picks the match up from its own snapshot.
        steps.Add(new JsonObject
        {
            ["kind"] = "snapshot",
            ["savePayload"] = Convert.ToBase64String(resolver.SavePayload()),
            ["hash"] = resolver.StateHash,
        });
    }
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

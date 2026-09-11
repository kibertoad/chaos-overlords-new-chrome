# Rechaos.Multiplayer

The game's client for the coordination server in [`multiplayer/`](../../multiplayer/README.md).

The server holds no game rules. It is the turn barrier and the record of what was ordered: it seals
each simultaneous turn, relays the order set, and compares the state hash every client reports. That
makes this project small and its correctness narrow — everything here exists so that two clients
given the same sealed set arrive at the same state, and can prove it to a server that cannot check
for them.

## Layout

| Directory | What is in it |
|---|---|
| `Generated/` | The wire types and route templates, generated from `multiplayer/packages/contracts`. Not edited by hand; see below. |
| `Protocol/` | Canonical JSON, the order digests, and the serializer settings every payload goes through. |
| `Http/` | The REST client, the typed refusal, a resumable server-sent event stream, and one definition of which failures are worth another attempt. |
| `Session/` | Bootstrapping a match from the server's seed, applying a sealed turn, and the two sessions that drive the lobby and the barrier. |

## The parts worth reading first

**`Protocol/CanonicalJson.cs`** is the reason a C# client can verify a digest a TypeScript server
computed. The rules are the server's — keys sorted by UTF-16 code unit, no whitespace, integers
written plainly — and the restriction to integers is the point: a float's shortest round-trip
spelling is not portable between the two runtimes, so a value with no shared text form throws rather
than hashing something no peer could reproduce. `MultiplayerCanonicalJsonTests` pins the same golden
document, canonical text and digest the server's own suite pins.

**`Session/SealedTurnApplier.cs`** is the whole of the lockstep contract: every player's ops in slot
order under the slot the seal attributed them to, every computer-controlled slot with no document
planned by the deterministic AI, then Execution, Hire, Elimination and Upkeep. The hash it returns is
what gets reported. Which slots those are is read out of the match being applied, never passed in — it
is hashed state, so it is the one answer every client is already guaranteed to agree on.

**`Session/SpeculativeTurn.cs`** is why the interface can show a queued command before the turn
seals without putting this client ahead of its peers.

**`Http/RetryPolicy.cs`** is where "worth another attempt" is decided, once, for both the event
stream and the calls a received event leads to. Before it was shared, a single dropped connection
while fetching a sealed set ended the match; every call it covers is idempotent, which is what makes
asking again the right answer rather than a risk.

**`Session/MultiplayerMatchSession.cs`** is the barrier: two background tasks, one reading the log and
one sending this player's document, both answering through a queue the game thread drains. Nothing
here blocks a caller, and nothing ends a match because the network hiccuped.

## Regenerating the wire types

```sh
cd multiplayer && pnpm codegen
```

`Generated/WireContracts.cs` comes from the valibot schemas through
[`@game-infra/valibot-to-csharp`](https://www.npmjs.com/package/@game-infra/valibot-to-csharp), and
`Generated/RouteTemplates.cs` from the endpoint contracts. Both are committed so that building the
game never needs Node; CI runs `pnpm codegen:check` to fail when either has drifted.

## Dependencies

`Rechaos.Core`, for the match state, the deterministic turn resolution and the native save format.
Nothing else: the HTTP client and JSON reader are the framework's.

## Consumers

`Rechaos.Game`, which owns the lobby screens and routes a player's mutations through `MatchActions` —
whose `HotSeatRecorder` refuses to hand out a recorder in an online match, because a call site that
got one would apply a command locally and never record it as an order, and the two look identical at
the point of the call.
`tools/OnlineSmoke` plays a short match against a running server with two clients in one process,
which is the end-to-end check the .NET test suite cannot make on its own.

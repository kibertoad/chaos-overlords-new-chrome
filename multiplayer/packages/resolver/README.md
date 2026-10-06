# @chaos-overlords/resolver

The turn resolver of the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome)
multiplayer server: the game's C# rules (`Rechaos.Core` and the client's sealed-turn path), compiled
to WebAssembly, with a host for Node.js and one for Cloudflare Workers. Given the facts the server
stores for a match (the `match.started` seed, settings and roster, every sealed order set, every seat
handover), it reaches the same state, and the same state hash, as every client. The design and the
measurements are in
[`docs/MULTIPLAYER.md`, "Resolving turns on the server"](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md#resolving-turns-on-the-server).

## Install

```sh
npm install @chaos-overlords/resolver
```

Requires Node.js 22 or newer. The package carries the WebAssembly build in `bundle/`, about 6.5 MB.

## Node

The Node host runs the build in a worker thread, because a turn costs hundreds of milliseconds of CPU
that must not stall the server's event loop.

```ts
import { startNodeMatchResolver } from '@chaos-overlords/resolver/node'

const resolver = await startNodeMatchResolver({ maxMatches: 64 })
await resolver.bootstrap(matchId, { seed, gameSettings, players })
const { stateHash } = await resolver.applySealedTurn(matchId, sealedOrders)
const checkpoint = await resolver.snapshot(matchId) // { body, stateHash }, the archive clients read
await resolver.restore(matchId, checkpoint)
await resolver.close()
```

`sealedOrders` is the body `GET /turns/:n/orders` answers, `gameSettings` the stored settings blob
and `players` the roster as `PlayerView` rows. `startNodeResolverHost` is the same host with bare save
payloads in place of archives.

## Cloudflare

The Cloudflare host is a Worker of its own, with a Durable Object per match, which the coordination
Worker calls over a service binding. It needs the Workers Paid plan: a turn takes about half a second
of CPU, against the 10 ms the free plan allows. A deployment without it keeps settling turns by the
hashes the clients report.

1. Deploy the resolver Worker from [`worker/wrangler.toml`](worker/wrangler.toml), with `main` set to
   this package's `worker/index.js` and `base_dir` to the package directory. It must run without the
   `nodejs_compat` flag.
2. Bind it to the coordination Worker as a service named `RESOLVER`:

   ```toml
   [[services]]
   binding = "RESOLVER"
   service = "chaos-overlords-resolver"
   ```

3. In the coordination Worker, which has `nodejs_compat` for the archive's Brotli:

   ```ts
   import { cloudflareMatchResolver } from '@chaos-overlords/resolver/cloudflare'

   const resolver = cloudflareMatchResolver(env.RESOLVER)
   ```

Durable Objects of one class share isolates, and an isolate has 128 MB, so the matches of every
object in an isolate share its limits: four matches and 24 MiB of managed heap by default, set with
the `RESOLVER_MAX_MATCHES` and `RESOLVER_MANAGED_HEAP_MIB` vars.

## Errors

A host holds a bounded number of matches and releases the least recently used. A call for a match it
does not hold rejects with `MatchNotHeldError` (code `match_not_held`): rebuild the match from its
newest checkpoint and the facts after it. A call whose input the build refuses, such as a sealed set
for another turn or a snapshot that does not hash to the state it is stored under, rejects with
`ResolverRefusedError` (code `resolver_refused`). Across the service binding both arrive as plain
errors; `resolverErrorCode(error)` reads the code back.

## Versions

A build plays one session version, the one `describe()` reports and `bundle/manifest.json` records.
A server refereeing matches stored under an older session version runs the release built for it
beside the current one, under an alias:

```json
"dependencies": {
  "@chaos-overlords/resolver": "<the current release>",
  "resolver-previous-session": "npm:@chaos-overlords/resolver@<the release built for that session version>"
}
```

## Building from source

`pnpm bundle` publishes `src/Rechaos.Resolver.Wasm` with the .NET SDK (set `DOTNET` to pick the
executable) and lays it out in `bundle/`; `pnpm build` compiles the hosts. The host tests replay a
match the native build played, written by `tools/ResolverDeterminism`; they skip without a bundle or
the SDK unless `REQUIRE_RESOLVER=1`, and `RESOLVER_TRANSCRIPT` names a transcript already written.

## Licence

MIT. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).

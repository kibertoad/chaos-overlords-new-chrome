# ResolverDeterminism

Holds the WebAssembly turn resolver (`src/Rechaos.Resolver.Wasm`) to the native build's state
hashes. It is the evidence behind the decision to resolve turns on the server with a WebAssembly
build of the rules (`docs/DECISIONS.md`, `docs/MULTIPLAYER.md`, "Resolving turns on the server"),
and the multiplayer workflow runs it on every change to the rules or the resolver.

The resolver runs here in the hosts of `multiplayer/packages/resolver`, the ones the coordination
server uses, so the package is bundled and built first:

```sh
cd multiplayer && pnpm install && pnpm --filter @chaos-overlords/resolver bundle && pnpm --filter @chaos-overlords/resolver build && cd ..
dotnet run --project tools/ResolverDeterminism -c Release -- transcript.json [seed] [turns] [duration]
node tools/ResolverDeterminism/check.mjs transcript.json [node|workerd]
```

| File | What it does |
|---|---|
| `Program.cs` | Plays a two-human match natively, once as a client plays it and once through `AuthoritativeMatch`, and fails if the two disagree. The human seats order what the computer would order for them; slot 1 goes to the computer before turn 4 and back before turn 7. The resolver is fed the match's event log as the server stores it, including events that change nothing, and the transcript holds every event with its sealed set and the hash after it, and a snapshot taken after turn 10, both as the archive a client uploads and as the bare save payload. It fails when the run stops before turn 10, since `check.mjs` would then never restore a match. At the end a match picked up from that snapshot folds the whole log from its start, as a reconnecting client does, and must reach the final hash. A longer `duration` (`TwoYears`, for one) makes a transcript for measuring memory over a long match. With `--read-archive <file> <hash>` it reads a snapshot archive a host wrote, as a client does, and fails unless it restores to the hash. |
| `check.mjs` | Replays a transcript through the Node host and through the Cloudflare host under workerd (Miniflare), with the replay in the package's `test/harness/replay.mjs`, and lists every hash that differs. From the snapshot step on it also drives a second match restored from the native archive, and at the end a third, restored from it, folds the whole log from its start. It then hands each host's own snapshot to `Program.cs --read-archive`, and prints boot time, time spent resolving and the size of the WebAssembly memory. |

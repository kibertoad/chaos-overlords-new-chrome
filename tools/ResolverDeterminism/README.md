# ResolverDeterminism

Holds the WebAssembly turn resolver (`src/Rechaos.Resolver.Wasm`) to the native build's state
hashes. It is the evidence behind the decision to resolve turns on the server with a WebAssembly
build of the rules (`docs/DECISIONS.md`, `docs/MULTIPLAYER.md`, "Resolving turns on the server"),
and the multiplayer workflow runs it on every change to the rules or the resolver.

```sh
dotnet build src/Rechaos.Resolver.Wasm -c Release
dotnet run --project tools/ResolverDeterminism -c Release -- transcript.json [seed] [turns]
node tools/ResolverDeterminism/check.mjs src/Rechaos.Resolver.Wasm/bin/Release/net10.0/wwwroot/_framework transcript.json [node|workerd]
```

| File | What it does |
|---|---|
| `Program.cs` | Plays a two-human match natively, once as a client plays it and once through `AuthoritativeMatch`, and fails if the two disagree. The human seats order what the computer would order for them; slot 1 goes to the computer before turn 4 and back before turn 7. It writes every sealed set, handover and hash to the transcript, with a save payload taken after turn 10, and fails when the run stops before turn 10, since the driver would then never restore a match. |
| `driver.mjs` | Replays a transcript through the resolver's exports and lists every hash that differs. From the snapshot step on it also drives a second match restored from the native payload. |
| `check.mjs` | Runs the driver under Node in process and under workerd through wrangler (resolved from `multiplayer/runtimes/cloudflare`, so run `pnpm install` in `multiplayer/` first), and prints boot time, time spent resolving, wall time and the size of the WebAssembly heap. workerd's clocks advance only on I/O, so under workerd only the wall time is printed. |
| `bundle-workerd.mjs`, `workerd-entry.mjs` | Lay the build out as workerd modules and boot it there. The header of `bundle-workerd.mjs` lists what workerd needs that the loader does not do by itself. |

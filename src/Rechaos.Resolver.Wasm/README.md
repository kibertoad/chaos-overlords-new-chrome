# Rechaos.Resolver.Wasm

The coordination server's turn resolver: `Rechaos.Core` and the client's sealed-turn path, built
for the .NET `browser-wasm` runtime so that the TypeScript server can run the same rules as the
game. The design is in [MULTIPLAYER.md](../../docs/MULTIPLAYER.md#resolving-turns-on-the-server)
and the decision in [DECISIONS.md](../../docs/DECISIONS.md).

`ResolverExports.cs` is a thin `[JSExport]` layer over
`Rechaos.Multiplayer.Resolution.AuthoritativeMatch`. Each export takes and returns the wire's JSON,
so a host passes the payloads it already stores (the settings blob, the roster, a sealed set) as
they are.

The project is not in `Rechaos.slnx`: restoring it downloads the browser-wasm runtime pack, and the
game has to build offline. Publish it with

```sh
dotnet publish src/Rechaos.Resolver.Wasm -c Release
```

which writes the trimmed bundle to `bin/Release/net10.0/publish/wwwroot/_framework`: about 3.2 MB of
assemblies and a 3.0 MB runtime. No workload is needed; the bundle runs the assemblies on the Mono
interpreter. Trimming works because `Rechaos.Core` and `Rechaos.Multiplayer` read and write JSON
through source-generated contracts (`CoreJsonContext`, `WireJsonContext`) and are marked
AOT-compatible, so the analyzers fail their build on a call the trimmer would break. `tools/ResolverDeterminism` checks that the
bundle reaches the same state hashes as the native build, under Node and under workerd.

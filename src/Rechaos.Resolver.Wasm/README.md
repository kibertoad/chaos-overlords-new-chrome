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
game has to build offline. Build it with

```sh
dotnet build src/Rechaos.Resolver.Wasm -c Release
```

which writes the bundle to `bin/Release/net10.0/wwwroot/_framework`. No workload is needed; the
build runs the assemblies on the Mono interpreter. `tools/ResolverDeterminism` checks that the
bundle reaches the same state hashes as the native build, under Node and under workerd.

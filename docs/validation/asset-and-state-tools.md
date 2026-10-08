# Asset and state tools

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

Validate a legal original installation without writing anything:

```powershell
dotnet run --project src/Rechaos.Extractor -- --verify-source `
  --source "C:\GOG Games\Chaos Overlords"
```

Fully rehash an installed output pack:

```powershell
dotnet run --project src/Rechaos.Extractor -- --verify-output `
  --output src/Rechaos.Game/Assets
```

`--quick` skips content hashes and checks manifest structure, safe paths,
presence, and lengths only. It is a startup optimization, never parity evidence.

Add `--json` to `--verify-output` for a versioned automation report on standard
output. Schema version 1 includes the absolute asset root, quick/full mode,
expected and manifest format/counts, verified-file count, and stable diagnostic
code/message/path/expected/actual records. Success and failure retain exit codes 0 and 1.
Examples include `asset_missing`, `asset_size_mismatch`, `asset_hash_mismatch`,
`unexpected_asset`, and `manifest_missing`.

Generate the asset catalog only from a pack that passes full verification:

```powershell
dotnet run --project src/Rechaos.Extractor -- --catalog `
  --output src/Rechaos.Game/Assets `
  --catalog-output docs/ASSET-CATALOG.md
```

Re-run the full paired-pixel RGB555/RGB565 comparison:

```powershell
dotnet run --project src/Rechaos.Extractor -- --analyze-px `
  --output src/Rechaos.Game/Assets
```

Compare two sanitized mechanical-state captures:

```powershell
dotnet run --project src/Rechaos.Tools -- state-diff `
  expected-state.json actual-state.json `
  --labels docs/schemas/state-labels.example.json
```

The fixture contract is
[`schemas/reference-fixture.schema.json`](../schemas/reference-fixture.schema.json).
It pins the executable and source-pack hashes, experiment/finding identity,
initial and final state, commands, expected events, and phase-boundary hashes.
Only sanitized mechanical values belong in a checked-in fixture; original
pixels, media, saves of uncertain redistribution status, and narrative text do
not.

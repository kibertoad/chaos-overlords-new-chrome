# Development guide

## Run from source

Install the .NET 10 SDK and a legal GOG copy of *Chaos Overlords*, then run:

```powershell
dotnet run --project src/Rechaos.Extractor -- --source "C:\GOG Games\Chaos Overlords"
dotnet run --project src/Rechaos.Game
```

On Windows, `play.bat` performs both steps and uses the default GOG location.
Set `CHAOS_OVERLORDS_PATH` first if the legal copy is elsewhere. To keep assets
elsewhere, pass `--output` to the extractor and the same path as `--assets` to
the game. Extracted files are ignored by Git and must not be redistributed.

Extraction stages and fully verifies a new pack before replacing the installed
one. A complete matching pack is left untouched unless `--force` is supplied.

Verify source or installed assets without rewriting them:

```powershell
dotnet run --project src/Rechaos.Extractor -- --verify-source --source "C:\GOG Games\Chaos Overlords"
dotnet run --project src/Rechaos.Extractor -- --verify-output
dotnet run --project src/Rechaos.Extractor -- --verify-output --json
```

Regenerate the checked-in factual asset inventory from a fully verified pack:

```powershell
dotnet run --project src/Rechaos.Extractor -- --catalog
```

Regenerate the embedded non-expressive gameplay tables from the fingerprinted
legal source files:

```powershell
dotnet run --project src/Rechaos.Extractor -- --generate-game-data src/Rechaos.Core/GameData/original-data.json --source "C:\GOG Games\Chaos Overlords"
```

Generator format 1 writes deterministic UTF-8 JSON. The loader and generator
both enforce the canonical 22-site, 90-gang, and 64-item table shape, ordered
identifiers, special-site mapping, item category ranges, combat-media bounds,
and the eleven unused item sentinels.

Pass `--debug-phases` to the game to expose individual deterministic resolution
steps during development.

## Build and test

Run the repository's fast regression gate through its serialized entry point:

```powershell
./tools/Invoke-Validation.ps1
```

It stops a game launched from this checkout, serializes validation, and bounds
build parallelism, preventing overlapping sessions and locked game files.
Build-server reuse remains on for performance; the build output itself goes to a
fresh temporary root each run, so every run is a cold build over the checkout as
it stands. Installed copies of the game are not stopped. See [VALIDATION.md](VALIDATION.md)
for the explicit 53-case long-running/full-suite modes and build-server cleanup
option.

The repository's version lives in `version.txt` as a single `x.y.z` line. Every
assembly is stamped from it, the title screen and every report print it, and a
build fails on anything else in the file. The release workflow writes it, so a
local checkout only needs editing when a package has to carry a different
number; see [RELEASING.md](RELEASING.md).

## Repository projects

- `Rechaos.Core`: original-data parsers and platform-independent game state.
- `Rechaos.Extractor`: validates and transforms legally owned source assets.
- `Rechaos.Tools`: compares sanitized state captures.
- `Rechaos.SnapshotInspector`: validates an exported online snapshot and optionally replays an exported sealed-order timeline against its recorded hashes. It is deliberately offline: acquire production exports through the deployment runbook, then run `dotnet run --project tools/Rechaos.SnapshotInspector -- <snapshot-base64.txt> [sealed-orders.json]`. The export needs no trimming: a snapshot resumes on the turn after the one it was taken on, and the tool skips the earlier turns itself and refuses an export that cannot reach the snapshot's turn.
- `Rechaos.Game`: MonoGame DesktopGL client.
- `Rechaos.Tests`: format, gameplay, persistence, extractor, and UI tests.

Detailed architecture, validation, reverse-engineering, format, and parity notes
live in the other files in this directory; [README.md](README.md) catalogs them
by purpose and indexes them by topic. Start a resumed development session with
[HANDOVER.md](HANDOVER.md).

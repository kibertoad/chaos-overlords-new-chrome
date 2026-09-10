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
```

Regenerate the checked-in factual asset inventory from a fully verified pack:

```powershell
dotnet run --project src/Rechaos.Extractor -- --catalog
```

Pass `--debug-phases` to the game to expose individual deterministic resolution
steps during development.

## Build and test

Run the repository's complete local gate through its serialized entry point:

```powershell
./tools/Invoke-Validation.ps1
```

It stops a game launched from this checkout, serializes validation, and bounds
build parallelism, preventing overlapping sessions and locked game files.
Normal incremental outputs and build-server reuse remain on for performance.
Installed copies of the game are not stopped. See [VALIDATION.md](VALIDATION.md)
for the explicit build-server cleanup option.

## Repository projects

- `Rechaos.Core`: original-data parsers and platform-independent game state.
- `Rechaos.Extractor`: validates and transforms legally owned source assets.
- `Rechaos.Tools`: compares sanitized state captures.
- `Rechaos.Game`: MonoGame DesktopGL client.
- `Rechaos.Tests`: format, gameplay, persistence, extractor, and UI tests.

Detailed architecture, validation, reverse-engineering, format, and parity notes
live in the other files in this directory. Start a resumed development session
with [HANDOVER.md](HANDOVER.md).

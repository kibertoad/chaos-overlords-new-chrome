# Re: Chaos Overlords

A clean-room MonoGame reimplementation of the 1996 turn-based strategy game.
This repository intentionally contains **no original game assets**. You must own
a supported legal copy; the extractor validates its asset pack (the executable
is neither required nor copied) and creates a local asset
pack containing repaired graphics plus the original audio, music, video, help,
and currently opaque resources. Compact gameplay tables
are bundled in the open-source core; original art and media are not.

## Quick start

Requirements: .NET 10 SDK and a GOG installation of *Chaos Overlords*.

```powershell
dotnet run --project src/Rechaos.Extractor -- --source "C:\GOG Games\Chaos Overlords"
dotnet run --project src/Rechaos.Game
```

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

On Windows, [`play.bat`](play.bat) performs both steps and uses the default GOG
location. Set `CHAOS_OVERLORDS_PATH` first if your legal copy is elsewhere.

To keep assets elsewhere, pass `--output` to the extractor and the same path as
`--assets` to the game. Extracted files are ignored by Git and must not be
redistributed.

## Current playable slice

The port loads the original 16-bit artwork at native 640x460 resolution with
integer-friendly point scaling, including ownership-composited `PX1000x` city
layers. It parses all 22 sites, 90 gangs, and 64 items and provides title,
new-game setup, and deterministic 8x8 hot-seat city screens.
Setup supports scenario, duration, and one-to-six human/computer players using
keyboard or mouse; click a player slot or press 1-6 to toggle its controller.
The default two-player setup is Human vs Computer. In the city, use arrows/WASD
or click to select, double-click a sector for its detailed view, Enter to act,
G to cycle gangs, C to open the legal-command picker, H to open the original
three-column hire comparison during planning (including dock snubbing), I for
the clickable 3x3 detailed-sector neighborhood, its three buildings, and
one-off/repeating gang-order controls, F for the
next-upkeep financial projection, R for scenario ranking, T for research and
equipment, B for the combat summary, and Space to finish planning. Recruit
portraits remain in the lower-right city dock and can be dragged onto a
controlled sector. Resolution phases run automatically in normal play; pass
`--debug-phases` to expose individual deterministic phase steps.
X opens the selected sector's detection-filtered gang search.
In the equipment panel, V opens the legal same-sector recipient list for an
equipped item; choose a gang with arrows or mouse and press Enter to queue Give.
The mapped city-panel buttons also open gang, sector, finance, and ranking views;
gang, hire, and site panels use the original portrait sheets.
Resolved equipped-weapon attacks play their original extracted `SND005xx` cue.
F5/F9 save/load and F6/F10
save/verify replays. Escape returns to the title screen before quitting.

## Projects

- `Rechaos.Core`: original-data parsers and platform-independent game state.
- `Rechaos.Extractor`: fingerprints the original asset pack, repairs PX16 BMP
  headers, and installs the original media/resources locally.
- `Rechaos.Tools`: compares sanitized JSON state captures with stable paths and
  optional human-readable field labels.
- `Rechaos.Game`: MonoGame DesktopGL client with no Content Pipeline dependency.
- `Rechaos.Tests`: format and extractor regression tests.

The format work was informed by the separate `re-chaos` reverse-engineering
notes; no source code or copyrighted resources are copied from the game.

All discoveries, evidence, uncertainty, and open format questions are tracked
in [`docs/ORIGINAL-FILE-FORMATS.md`](docs/ORIGINAL-FILE-FORMATS.md).
The generated inventory covers 471 original resources and all 685 installed
outputs (including decoded PX08 derivatives) in
[`docs/ASSET-CATALOG.md`](docs/ASSET-CATALOG.md).
The complete migration sequence and parity gates are tracked in
[`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md).
Current boundaries and validation procedures are documented in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md),
[`docs/VALIDATION.md`](docs/VALIDATION.md), and
[`docs/PARITY-MATRIX.md`](docs/PARITY-MATRIX.md).
The recreation-native versioned snapshot schema is documented in
[`docs/NATIVE-SAVE-FORMAT.md`](docs/NATIVE-SAVE-FORMAT.md).
Clean-room findings from the fingerprinted original executable are recorded in
[`docs/ORIGINAL-INTERNALS.md`](docs/ORIGINAL-INTERNALS.md).
The reproducible temporary-project workflow for the locally installed Ghidra
tooling is documented in [`docs/GHIDRA.md`](docs/GHIDRA.md). On the known
research machine, use the pinned installation at
`C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC`; do not
search for or reinstall Ghidra before checking that path.

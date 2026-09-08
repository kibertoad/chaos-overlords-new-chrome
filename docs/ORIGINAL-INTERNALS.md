# Original executable internals research

Status: active clean-room research log
Last updated: 2026-09-08
Reference executable SHA-256:
`a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`

This document records factual structure and testable interpretations of the
original executable. It does not contain copied decompiled source. Addresses are
valid only for the fingerprint above.

## Finding format

- **ID**: stable reference used by rules, code, tests, and the parity matrix.
- **Observation**: what is directly present in the executable or behavior.
- **Interpretation**: what the observation may mean.
- **Confidence**: Verified, High, Medium, or Low.
- **Next validation**: experiment needed before relying on semantics.

## PE image

### BIN-PE-001 - executable format

**Observation:** The file is PE32 for Intel x86 (`Machine = 0x14c`), Windows GUI
subsystem, with preferred image base `0x00400000`, entry point `0x00478d00`,
image size `0x000c9000`, and no debug directory. Symbols, COFF line numbers, and
the symbol table are stripped. Linker version is 3.10. The COFF timestamp is
1996-07-19 22:26:46 in the dumper's local display.

**Interpretation:** This is a native 32-bit Windows build consistent with the
original 1996 release, not a managed GOG launcher.

**Confidence:** Verified for header values; High for interpretation.

### BIN-PE-002 - sections

| Section | RVA | Virtual size | Raw offset/size | Flags |
|---|---:|---:|---:|---|
| `.text` | `0x1000` | `0x7fac2` | `0x400` / `0x7fc00` | execute, read |
| `.rdata` | `0x81000` | `0xc4d` | `0x80000` / `0xe00` | read |
| `.data` | `0x82000` | `0x2b110` | `0x80e00` / `0x7a00` | read, write |
| `.idata` | `0xae000` | `0x19fa` | `0x88800` / `0x1a00` | read, write |
| `.rsrc` | `0xb0000` | `0x11bec` | `0x8a200` / `0x11c00` | read |
| `.reloc` | `0xc2000` | `0x6498` | `0x9be00` / `0x6600` | discardable, read |

**Confidence:** Verified from PE headers.

The large zero-initialized tail of `.data` is a candidate home for the global
match arrays described by the save format. This is only a hypothesis until
cross-references and runtime/save correlations are established.

## Platform boundaries visible in imports

### BIN-API-001 - rendering

**Observation:** The binary imports `DirectDrawCreate` from `DDRAW.dll`, plus GDI
palette/DIB/blit operations including `SetDIBits`, `BitBlt`, `StretchBlt`,
`CreatePalette`, `SetPaletteEntries`, `RealizePalette`, `UpdateColors`, and
`SetSystemPaletteUse`. USER32 imports include cursor, bitmap, menu, dialog,
window, key-state, paint, and message-loop functions.

**Interpretation:** Original rendering combines DirectDraw with Win32/GDI and
explicit palette management. The PX08/PX16 split and color-depth preference are
selected within this platform layer.

**Confidence:** Verified imports; Medium architecture interpretation.

**Next validation:** Find cross-references from the literal PX paths and palette
APIs, then map the load/convert/blit functions and color-key behavior.

### BIN-API-002 - audio and video

**Observation:** WINMM imports include `PlaySoundA`, `mciSendCommandA`, auxiliary
volume APIs, `timeGetTime`, `timeSetEvent`, and `timeKillEvent`. Sixteen Smacker
functions are imported by ordinal from `smackw32.dll`.

**Interpretation:** Effects likely use `PlaySoundA`, CD/music control likely uses
MCI, and Smacker owns intro/logo decoding. Multimedia timers may drive animation
or sound; their presence does not prove simulation timing.

**Confidence:** Verified imports; Medium API-role interpretation; Low timer role.

**Next validation:** Cross-reference `data\snd00000`, `Data\mvIntro`,
`Data\mvLogos`, and `A:\CHAOS\CDTrack` literals.

### BIN-API-003 - files and persistence

**Observation:** The executable imports `CreateFileA`, `ReadFile`, `WriteFile`,
`GetFileSize`, `SetFilePointer`, `FlushFileBuffers`, `GetOpenFileNameA`, and
`GetSaveFileNameA`. Embedded strings include `Save Files (*.SAV)`, `Please
specify save name`, and `Old Version of Saved Game.`

**Interpretation:** Save/load is implemented with direct Win32 file I/O and a
version/magic branch consistent with the two documented save variants.

**Confidence:** Verified observations; High interpretation.

**Next validation:** Locate string references, identify read/write functions,
and match their fixed transfer sizes against the save-layout document.

### BIN-API-004 - legacy networking

**Observation:** The import table contains 18 WinSock functions by ordinal,
Windows Telephony API calls for modem setup/dialing, serial-port configuration,
overlapped file I/O, communication masks/events, threads, mutexes, critical
sections, and synchronization waits. Embedded strings reference modem Control
Panel setup and host windows.

**Interpretation:** WinSock, modem, and serial transports described by the manual
are native subsystems in this build and share synchronization infrastructure.

**Confidence:** Verified imports/strings; High interpretation.

**Security decision:** Original transports are research-only and must not be
exposed to untrusted networks. A recreation transport will not reuse this code
or wire format without a separate protocol/security study.

### BIN-API-005 - configuration

**Observation:** Registry APIs are imported. Strings include
`SOFTWARE\Stick Man Games\Chaos Overlords\1.0` and the Windows App Paths key for
`Chaos Overlords.exe`.

**Interpretation:** Preferences and/or installation location are stored in the
registry under the product key.

**Confidence:** Verified strings/imports; Medium interpretation.

**Next validation:** Inspect registry reads/writes in a disposable reference VM
while changing one option at a time.

## Resource lookup literals

### BIN-ASSET-001 - data paths

The following case-varying format/path literals are embedded in the executable:

| Literal | Likely role | Confidence |
|---|---|---|
| `data\Sites` | site definitions | High |
| `data\Gangs` | gang definitions | High |
| `data\Items` | item definitions | High |
| `data\PX08\PX00000` | formatted 8-bit image lookup | High |
| `data\PX08\px00128` | fixed 8-bit resource | High |
| `data\PX16\px00128` | fixed 16-bit resource | High |
| `data\snd00000` | formatted sound lookup | High |
| `Data\mvIntro` | intro movie | High |
| `Data\mvLogos` | logo movie | High |
| `.\Help\Chaos.hlp` | WinHelp content | Verified |
| `A:\CHAOS\CDTrack` | CD music path/template | Medium |

The `00000` suffix strongly suggests integer-to-five-digit resource formatting,
but the formatter and valid ranges must be located before this is marked
Verified.

## Timing and RNG candidates

### BIN-RNG-001 - imported clocks

**Observation:** `GetTickCount`, `timeGetTime`, periodic multimedia timer APIs,
and asynchronous key state are imported. No external C runtime DLL appears in
the import table, so any C library RNG would be statically linked.

**Interpretation:** One clock may seed random state, but any may instead be used
only for UI animation, input, networking, or audio timing.

**Confidence:** Verified observation; Low RNG interpretation.

**Next validation:** Follow clock return-value data flow to determine whether
either clock seeds gameplay state or is presentation-only.

### BIN-RNG-002 - runtime random step

**Observation:** Ghidra 12.1.3 identifies the function at virtual address
`0x00478cd0` as the statically linked Visual Studio 1998 `_rand`. It updates the
calling thread's 32-bit hold state using multiplier `0x343fd` and addend
`0x269ec3`, then returns bits 16-30 of the new state. Ghidra found one direct
game caller, at `0x0045d227`.

**Interpretation:** The recurrence and 15-bit output are the original build's
raw random-number step.

**Confidence:** High from function identification, constants, state write, and
single-caller cross-reference. Runtime output still needs black-box correlation.

### BIN-RNG-003 - bounded random wrapper

**Observation:** The function at `0x0045d227` clamps an input below one to one,
calls `_rand` three times, uses the third result as a selector, chooses the first
result when the selector is greater than `0x3ffe` and otherwise the second, then
returns the chosen value modulo the clamped input plus one.

**Interpretation:** Gameplay requests an inclusive random integer from one
through the input and consumes exactly three raw RNG values for each request.

**Confidence:** High for control flow, constants, range, and consumption count.
The initial hold-state seed and complete wrapper call-site ownership remain
unknown.

**Implementation:** `DeterministicRandom.NextRaw` and `NextInclusive` reproduce
these address-level facts. `MatchSetup.InitialSeed` remains a recreation input,
not a verified mapping to the original hold state.

**Next validation:** Locate writes to the per-thread hold state and all callers
of `0x0045d227`; correlate a controlled dice sequence with predicted outputs.

Ghidra reports direct calls to `0x0045d227` from 21 containing functions and 61
call sites. This establishes broad reuse but does not yet assign gameplay
semantics to individual callers. The checked-in focused-report script now emits
incoming call addresses to support that mapping without storing bulk decompiler
output.

### BIN-RNG-004 - three callers classified as command selection, not combat

**Observation:** Focused Ghidra 12.1.3 summaries of RNG-wrapper callers
`0x00401000`, `0x0040abc0`, `0x00428ef0`, and `0x00436c70` show large action-selection
switches, repeated bounded attempts to choose targets, and writes to per-gang
command/target slots. All three are reached from `0x00432da0`; none directly
applies Force damage or exhibits the manual's attack/retaliation arithmetic.

**Interpretation:** These functions belong to AI command planning or validation,
not the Combat resolver. They should be excluded from the next formula search;
their shared caller `0x00432da0` is a promising AI dispatcher candidate.

**Confidence:** Medium. The command-slot interpretation is structurally strong,
but field identities and action constants are not yet fully labeled.

**Next validation:** inspect the remaining RNG callers for writes to the gang
Force field and for paired full/halved success loops, then correlate the result
with a controlled original-game combat observation.

## Toolchain hypothesis

### BIN-TOOL-001 - compiler/runtime

**Observation:** Linker version 3.10, 1996 timestamp, no imported MSVCRT DLL, and
native Win32 APIs.

**Interpretation:** A mid-1990s Microsoft Visual C++ toolchain with statically
linked runtime is plausible.

**Confidence:** Medium. Linker fingerprints and startup code still need matching
against known toolchain signatures.

## Priority static-analysis queue

1. Xrefs to save/version strings and fixed file transfer sizes.
2. Xrefs to `data\Sites`, `data\Gangs`, and `data\Items`; map load destinations.
3. Phase dispatcher using action IDs 0-14 and execution ordering.
4. `GetTickCount`/`timeGetTime` xrefs and candidate PRNG recurrence.
5. Dice range reduction and success-count loop.
6. Control, influence, chaos, heal, combat, stealth, and crackdown resolvers.
7. Scenario setup/scoring/victory table and turn limits.
8. City/site distribution and HQ placement.
9. AI command-selection entry points and difficulty branches.
10. PX/SND/MV formatters and semantic resource-ID tables.

Every completed item must add address-level findings here, black-box fixtures in
the validation ledger, and a linked parity-matrix update.

# Original executable internals research

Status: active clean-room research log
Last updated: 2026-09-09
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

### BIN-UI-001 - combat animation cadence

**Observation:** `FUN_0042e040` references the four combat-strip resource bases:
7000 at `0x0042e812`, 7100 at `0x0042e86d`, 7300 at `0x0042eb41`, and 7200 at
`0x0042eb9f`, then enters the presentation loop in `FUN_00430c23`. That loop
reads and clears timer slot zero and advances its local phase; cases 3 through
10 render the eight consecutive 64-pixel frames. Cases 13 and 15 draw the
changed portions of the two force bars in white, cases 14 and 16 restore the
bar areas, and cases 17 through 21 retain the completed result before case 22
exits. During initialization,
`FUN_00460ccf` calls `FUN_004327dc(0, 6)`. The timer helper configures
`timeSetEvent` with an integer period of `1000 / rate` milliseconds.

**Interpretation:** Combat presentation advances at 6 Hz: 166 milliseconds per
frame in the original integer timer configuration, or about 1.33 seconds for
one eight-frame attack/hit clip. Damage removed from each force bar flashes
white twice before settling into the missing-force color, followed by a
five-tick result hold.

**Confidence:** High static evidence. The recovered timer setup, timer-slot use,
and frame-phase sequence agree; behavioral observation also identified the
previous recreation playback as too fast.

**Next validation:** Capture an original combat sequence with frame timestamps
to quantify any scheduling jitter around the 166-millisecond nominal period.

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

**Scope decision:** This import-level inventory is the endpoint for legacy
network research. Original transports and wire formats are explicitly outside
the recreation scope and will not be ported, exposed, or supported for
interoperability.

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

### BIN-AI-001 - per-gang command dispatcher and action handlers

**Observation:** Focused Ghidra 12.1.3 analysis identifies `0x00432da0` as a
dispatcher reached from `0x00458fa0` at call site `0x004594df`. Its two
arguments index arrays with player stride `0x510` and gang stride `0x10`. The
dispatcher writes a selected family byte at `0x0048a250 + player * 0x510 +
gang * 0x10`, then dispatches that value through the following handler table:

| Value | Handler |
|---:|---:|
| 0 | `0x00428ef0` |
| 1 | `0x00434080` |
| 2 | `0x0041fef0` |
| 3 | `0x00435bd0` |
| 4 | `0x00401000` |
| 5 | `0x0043a1d0` |
| 6 | `0x00431c60` |
| 7 | `0x00436c70` |
| 9 | `0x004605e0` |
| 10 | `0x0042a6e0` |
| 11 | `0x00420950` |
| 12 | `0x004353a0` |
| 13 | `0x0040abc0` |
| 14 | `0x00466910` |

No case for value 8 appears in this handler switch. Several handlers write the
chosen command and parameters both to a `0x10`-stride array rooted near
`0x0048a250` and to a second projection with player stride `0xa20` and gang
stride `0x20` rooted near `0x00498daf`. Target-sector results are repeatedly
split using quotient and remainder by `0x51` (81).

**Interpretation:** The first byte is an AI strategy/action-family selector,
the handler switch maps it to command planners, and the two projections are
closely related per-gang planned-command records. Division by 81 encodes a
player/sector or owner/sector pair. Exact field names and the correspondence
between family values and public command IDs are not yet established.

**Confidence:** Verified for addresses, call site, strides, switch values,
handler mapping, mirrored writes, and division constant; Medium for record and
target semantics; Low for public action-name mapping.

**Next validation:** analyze `0x00458fa0`, label the state-query selectors used
by `0x00432da0`, and correlate each handler with controlled queued commands.

### BIN-AI-002 - scenario-sensitive family selection

**Observation:** When a state query made by `0x00432da0` reports one, the
dispatcher performs a ten-way switch on values 0 through 9. Each branch then
switches on a second query whose observed results are 0 through 6 and maps that
pair to family values drawn from 0, 1, 2, 3, 5, 6, 7, 10, 11, 12, 13, and 14.
The same function contains separate explicit comparisons against global
`0x004abbe8` values 6, 7, and 8.

**Interpretation:** The ten-way selector is likely the ten scenario/objective
IDs and changes the preferred command family. `0x004abbe8` is also scenario-like
in this routine, so it must not be assumed to be AI difficulty merely because
it changes planning branches. The selector identities still require caller and
save/global correlation.

**Confidence:** Verified for branch shape, ranges, mapped values, and global
comparisons; Medium for scenario interpretation.

**Next validation:** correlate the query and global values against setup/save
fields, then locate the distinct four-valued AI Mentality state.

### BIN-AI-003 - outer AI planning pass and command history

**Observation:** `0x00458fa0`, the sole normal caller of the per-gang dispatcher,
is itself reached from `0x0040ab20` at `0x0040abac` and from the initialization
routine `0x0046e766` at `0x0046f4ec`. On first use for a player it calls
`0x00409de1` for all 81 indices and initializes per-player state. On subsequent
passes it iterates all 81 records with player stride `0x510` and record stride
`0x10`, copies bytes at offsets +5..+7 into +2..+4, clears +8..+10, and
decrements the two 16-bit fields at +12 and +14 only when their associated
queries are nonnegative. It skips records whose mirrored `0x20`-stride byte at
`0x00498daa` is decimal 100. It later iterates the same 81 records, seeds family
9 under a separate condition, and calls `0x00432da0` for every non-100 record.

After the per-record pass, `0x00458fa0` performs a ten-way switch on the same
state-query value 0 through 9 seen by `0x00432da0`. Each branch selects among
strategy values, calls `0x004078d9` with small mode values, and updates
per-player words at `0x00482128` and `0x00482140`. The function also iterates
64 entries in a separate sector-sized pass before dispatching gangs.

**Interpretation:** `0x00458fa0` is the outer AI planning pass. The +2..+4 and
+5..+7 triples are previous/current command projections, +8..+10 is the newly
planned triple, decimal 100 marks an unused gang slot, and +12/+14 are two
countdowns. The post-dispatch ten-way switch is objective strategy, while the
64-entry pass prepares sector-level priorities. These field meanings remain
provisional until save deltas or controlled commands identify them.

**Confidence:** Verified for callers, loop bounds, addresses, strides, copies,
clears, decrements, sentinel, and dispatcher call coverage; High that this is an
outer AI planner; Medium for command-history/countdown/strategy semantics.

**Next validation:** correlate the three byte triples and two words against a
saved recurring and one-off command, then trace the four-valued global setup
selection independently of the ten-way scenario selector.

### BIN-AI-004 - global AI Mentality byte and first consumers

**Observation:** The Win32 string table in EXE-GOG-1.1 maps resource IDs 46,
47, 48, and 49 to `GOON`, `CRIMINAL`, `CRIME LORD`, and `HOMICIDAL MANIAC`.
The setup presenter `0x0045519d` loads the displayed choice at call site
`0x00455502` using resource ID `46 + (signed byte)[0x00487850]`. In the central
state-query function `0x00402d70`, case `0x36` returns that same signed byte.
This distinguishes it from the scenario-like global `0x004abbe8` and from
query selectors `0x2f` and `0x31` used elsewhere in the planner.

The six focused writes now have bounded data-flow classifications:

- `0x00464618` is preference initialization. `0x0046439a` obtains the registry
  value named `prefsDiff` with `RegQueryValueExA` and copies its low byte into
  `0x00487850`.
- `0x00439542` is the setup-panel apply path. `0x00438da5` snapshots the global
  into a local selection, changes that local from the setup hit regions, and
  writes it back alongside the selected scenario and duration fields.
- `0x00461bf6` and `0x00461fea` are two commit paths in the same UI/event
  handler. The first path initially copies `0x00487850` to the one-byte staging
  field `0x0049833c`; both paths later copy that staging byte back.
- `0x00463bc7` and `0x00463bf1` restore the same one-byte staging field after
  `0x0046381a` serializes/deserializes it with adjacent setup fields. They are
  selected by two distinct four-byte format markers. Whether those formats are
  file, local IPC, or legacy-network envelopes is intentionally left unlabeled.

There are eight genuine selector-`0x36` calls, all in two functions: calls at
`0x0040a734` and `0x0040a7b8` in `0x0040a1a7`, plus calls at `0x0043466a`,
`0x004346d3`, `0x00434da6`, `0x00434dd9`, `0x00435090`, and `0x004350c3` in
family-1 handler `0x00434080`. A nearby call at `0x0040950f` is not a consumer:
its actual selector argument is `0x21`; `0x36` only appears in a preceding
comparison.

`0x0040a1a7` updates a player-pair table only when two positive pair fields
produce a ratio above 75 percent. Player-type values 0 or 3 enter that path only
at Mentality 1 or higher; all other type values enter it only below Mentality 2.
The successful path sets a pair flag and writes `-10` to a paired score field.
The player-type and pair-field meanings remain unlabeled.

The six family-handler calls form three paired gates. They select among record
action bytes 3, 10, and 13, with additional queries using selectors 3, 4,
`0x21`, and `0x35`. The exact tests include Mentality equal to zero, at least
one, and exactly two. Value 3 follows the `>= 1` paths but not the `== 2` paths;
there is no dedicated comparison for it in these blocks. Public command names
must not yet be assigned to those action bytes solely because the recreation's
enum currently uses the same numbers.

**Interpretation:** `0x00487850` is the original match-global, zero-based AI
Mentality setting, seeded from a persisted preference and then carried through
setup staging/serialization. Family handler 1 and a player-pair scoring pass
both change paths by mentality. The branch mechanics are now bounded, but their
state-query meanings and public command effects are not yet established.

**Confidence:** Verified for resource IDs, address, display expression, query
selector, all six write classifications, all eight genuine consumer call sites,
comparison constants, and resulting raw record writes; High for the global's
identity and persistence/setup flow; Low for state-query and public command
semantics.

**Next validation:** label selectors 3, 4, `0x21`, and `0x35`, then correlate
raw action bytes 3, 10, and 13 with controlled original queued-command
observations. Use those labels to turn the enumerated branch table into
observable command-selection fixtures before changing recreation policy.

## New-game initialization

### BIN-CITY-001 - density-derived sector income and tolerance

**Observation:** In EXE-GOG-1.1, `0x00475fe1` constructs a 32 by 32 integer
density field. Forty pairs of bounded `1..32` draws select zero-based centers.
Four nested-radius passes add clipped 2 by 2, 4 by 4 and 6 by 6 footprints,
capping cells at four. Each board sector sums the corresponding 4 by 4 cells.
Its income byte is rounded from the average and offset by three; its initial
tolerance byte is `17 - income`.

**Interpretation:** Generated base income is 3 through 7 and is independent of
the three sites' cash benefits. The recreation therefore stores sector income
explicitly rather than deriving it from site definitions.

**Confidence:** High static evidence; runtime reference fixture pending.

### BIN-CITY-002 - three-site rejection sampling

**Observation:** `0x004764b6` draws a site ID from 0 through 20. In scenario
index 9 it rejects IDs 4 and 8. `0x00475fe1` rejects duplicate second/third
sites, and `0x00476516` rejects a partial combination when the sum of any of
the 14 site statistic modifiers is outside -6 through +6.

**Interpretation:** Site frequency is not used by this generation path. Scenario
9 is Armageddon; its excluded Research Lab and Science Center agree with the
scenario's initially completed research.

**Confidence:** High static evidence; runtime reference fixture pending.

### BIN-CITY-003 - headquarters and Right Hands

**Observation:** `0x00439563` writes sector IDs 9, 12, 30, 33, 51 and 54.
`0x00476726` creates a six-value permutation by repeated bounded `1..6` draws
with duplicate rejection, maps players through that table, assigns ownership,
and replaces site slot zero with definition 21. `0x0046dc10` then initializes
gang definition zero in each player's assigned sector at Force 10.

**Interpretation:** Those six fixed sectors are the only new-game HQ candidates;
Right Hands is definition zero and always starts at maximum Force.

**Confidence:** High static evidence; active-player-count presentation and a
runtime reference fixture remain pending.

### BIN-HIRE-001 - initial and replacement offers

**Observation:** `0x0046e766` initializes all three offer bytes per internal
player to signed -100. Before a human interaction it calls `0x004716eb`, which
fills negative slots using repeated bounded `1..89` draws, rejecting duplicates
among the three slots and the positive ID represented by the replaced negative
slot. The hire resolver at `0x00472775` negates removed offers before refill and
creates a hired gang with a bounded `1..5` result plus four.

**Interpretation:** Initial offers are populated on first interaction. Replacement
selection is rejection sampling over gang IDs 1 through 89; a just-hired or
snubbed gang cannot immediately replace itself. Hired Force is uniformly 5
through 9 through the recovered bounded wrapper.

**Confidence:** High static evidence; panel timing and runtime sequences still
need a controlled original-game observation.

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

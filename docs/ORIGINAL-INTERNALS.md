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

**Observation:** Query selector 0 returns `DAT_004abbe8`; setup/save analysis
identifies this byte as the scenario ID in the same 0-through-9 order used by
`ScenarioId`. Query selector `0x7c` returns the per-player word at
`0x00482128 + player * 4`. When query `0x48` reports that the planning record's
byte at +1 is nonzero, `0x00432da0` maps scenario and query `0x7c`'s hire role to
the family below. A dash means the switch performs no assignment and preserves
the record's current family.

| Scenario | Mode 0 | Mode 1 | Mode 2 | Mode 3 | Mode 4 | Mode 5 | Mode 6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Greed | 0 | 0 | 3 | 2 | 6 | - | 7 |
| Power | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Acceptance | 0 | 0 | 5 | 2 | 6 | - | 7 |
| Dominance | 0 | 0 | 5 | 2 | 6 | 3 | 7 |
| Kill 'Em All | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Big 40 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| Eliminate | 0 | 13 | 14 | - | 6 | 5 | 7 |
| Siege | 10 | 0 | 3 | 11 | 12 | - | 7 |
| Big Man | 0 | 13 | 14 | 3 | - | - | - |
| Armageddon | 0 | 1 | 3 | 2 | 6 | 3 | - |

Every assigning mode-4 branch also copies the signed query-`0x5a` gang
projection into the planning record's +12 word. Out-of-range modes likewise
preserve the current family. `OriginalAiFamilyRules` implements this table and
side-effect flag in isolation. The same function separately compares the
scenario global with values 6, 7, and 8 after family dispatch.

**Confidence:** Verified for selector storage, scenario identity/order, table
values, preserve behavior, mode-4 copy, and global comparisons. The semantic
names of hire roles 0 through 6 remain unknown.

**Next validation:** recover the outer planner's family-count adjustments before
it writes the `0x7c` hire-role word, then represent planning records in
`MatchState`.

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
state-query value 0 through 9 seen by `0x00432da0`. Each branch starts from a
deterministic current-turn schedule, adjusts that slot against existing family
counts, calls `0x004078d9` with an offer-ranking mode, and writes the selected
hire role to `0x00482128`. The function also iterates
64 entries in a separate sector-sized pass before dispatching gangs.

**Interpretation:** `0x00458fa0` is the outer AI planning pass. The +2..+4 and
+5..+7 triples are previous/current command projections, +8..+10 is the newly
planned triple, decimal 100 marks an unused gang slot, and +12/+14 are two
countdowns. The post-dispatch ten-way switch is objective strategy, while the
64-entry pass prepares sector-level priorities. These field meanings remain
provisional until save deltas or controlled commands identify them.

**Confidence:** Verified for callers, loop bounds, addresses, strides, copies,
clears, decrements, sentinel, dispatcher call coverage, and the hire-role
schedule; High that this is an outer AI planner; Medium for command-history and
countdown semantics.

**Next validation:** correlate the three byte triples and two words against a
saved recurring and one-off command, then trace the four-valued global setup
selection independently of the ten-way scenario selector.

### BIN-AI-003B - base hire-role schedule

**Observation:** Before its family-count and affordability adjustments,
`0x00458fa0` seeds a schedule slot from the current turn (query `0x2f`). Nine
scenarios use `turn % 10`; Dominance uniquely uses `turn % 11`. Each table cell
below is `ranking mode / hire role`, directly matching the call argument to
`0x004078d9` and the dword written to `0x00482128` by the final switch.

| Scenario | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Greed | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 0/1 | 2/2 | 3/3 | - |
| Power | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Acceptance | 0/1 | 4/6 | 3/4 | 0/1 | 2/2 | 3/3 | 2/2 | 0/1 | 2/2 | 0/1 | - |
| Dominance | 0/1 | 0/1 | 2/5 | 2/2 | 4/6 | 3/3 | 2/2 | 0/1 | 2/5 | 0/1 | 3/4 |
| Kill 'Em All | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Big 40 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| Eliminate | 0/0 | 1/2 | 0/0 | 1/2 | 4/6 | 1/1 | 1/2 | 0/0 | 1/1 | 2/5 | - |
| Siege | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 3/4 | 2/2 | 5/3 | - |
| Big Man | 0/0 | 1/2 | 0/0 | 1/1 | 1/1 | 2/3 | 1/2 | 0/0 | 1/1 | 1/2 | - |
| Armageddon | 0/0 | 1/1 | 0/0 | 3/3 | 0/0 | 3/4 | 0/0 | 3/3 | 0/0 | 2/5 | - |

The Power, Kill 'Em All, and Big 40 switch bodies are identical. Static
constants at `0x00481018` onward decode as floats 52, 4, 2, 100, 3, and 6.
The planner computes `total duration turns / 52` and retains that factor in an
x87/local-stack value used by quota comparisons. Ghidra misleadingly renders
several later uses as multiplication by `0.0`; instruction windows confirm they
still consume the saved factor. `OriginalAiHireRoleRules` implements the exact
pre-adjustment schedule. It also implements the identical Power/Kill 'Em All/
Big 40 adjustment block: late turns remap slots 4 and 8; selector `0x9a`,
selector `0x5f`, the prior role, and family-5/family-7 presence redirect slot 6;
family counts cap slots 8, 6, 9, and 4 at respectively `factor * 4`,
`factor * 4`, `factor * 3`, and `factor`; and fewer than four family-0-or-4
gangs forces slot zero.
The same class now contains instruction-verified adjustments for every
scenario. These preserve strict versus inclusive quota
boundaries and original statement order: Siege's cash floor is strict and its
missing-family-6-or-12 fallback runs last; Big Man redirects four slots only
when the family-0-or-4 count is strictly above five; Armageddon's missing-family-2
override is last even after another rule has reset the slot. Greed, Acceptance,
and Dominance retain their distinct late-turn windows, selector-driven fallback
chains, quota multipliers, and Dominance's unique slot 10.

The enclosing attempt gates are also scenario-specific. Greed requires the
active gang count to be at or below its computed limit and requires remaining
turns to be strictly greater than integer `total duration / 8`. Power,
Acceptance, and Dominance require the inclusive gang limit plus more than two
remaining turns. Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon use only
the inclusive gang limit. Big Man enters its schedule without that limit check.
`OriginalAiHireRoleRules.ShouldAttemptHire` implements these boundaries.

The limit feeding those gates is now instruction-verified too. Selector
`0x22` returns one when any sector is neutral and not under Crackdown; selector
`0x23` counts sectors owned by the requested player. When no such neutral
sector remains, cash above 300 yields the fixed limit 80, while cash at or
below 300 yields `active gangs + owned sectors`. While a neutral sector does
remain, Greed uses the integer truncation of `owned sectors * 1.5`, Power,
Acceptance, and Dominance use `owned sectors * 2`, and scenarios 4 through 9
use `owned sectors * 4`. Every result is capped at 80. The `1.5` is the decoded
double at `0x00481010`; the emitted x87 conversion truncates the nonnegative
count. `OriginalAiHireRoleRules.CalculateHireGangLimit` implements the block.

Query `0x2f` is the zero-based elapsed-turn counter at `0x0049ca68`; query 2
returns total scenario duration minus that counter. The counter initializes to
zero and increments once after the outer planner loop, so recreation turn 1
feeds schedule slot zero and the full duration as turns remaining.

Selector `0x90` scans every other player's 81 gang slots for a gang whose
mirrored sector byte equals the requested sector and whose per-observer
visibility byte is set. It returns 10 when that gang's owner has state 0 or 3
and the observer-to-owner attitude is negative, 1 for another visible opposing
gang, and 0 when none exists. Selector `0x9a` returns the requested ordinal
sector whose cached `0x90` value is 10, or sentinel 100. With ordinal 1 in the
hire planner, it therefore identifies the first sector containing a visible
hostile opposing gang. Selector `0x5f` scans active family-6 gangs and returns
one whose current sector or cached destination matches that sector, otherwise
`-1`. The adjustment input now names these as visible-hostile-sector presence
and family-6 coverage rather than retaining raw selector numbers.

The remaining Greed-only byte read is also semantic. The scenario scorer at
`0x0047712a` stores, for each active player, the count of players with a
strictly greater scenario score at `0x004abc08 + player`; tied leaders therefore
both hold zero. In schedule slot 9, only a player with a nonzero value (behind
at least one higher-scoring player) is reset to slot 0 when fewer than ten
turns remain or cash is below 100. `OriginalAiHireAdjustmentInputs` exposes
that exact condition as `HasHigherScoringPlayer`.

**Confidence:** Verified for scenario order, periods, every ranking-mode call,
every hire-role write, shared scenario bodies, constants, retained factor, x87
comparison direction, equality boundaries, adjustment ordering, and the
Greed-only scenario-standing predicate, plus the complete hire-limit inputs,
multipliers, cash boundary, and cap.

The recreation's `AiPlanningState` now preserves the verified six current-role
words, six previous-role words, and six-by-81 family slots in canonical hashes,
native saves, and replays. Save/replay version 7 migrates earlier snapshots to
role zero and family sentinel 99 without advancing the RNG. `PrepareAiPlanning`
now performs the verified role rollover and applies the scenario/role family
table to each active gang before the existing hostility pass. The separate
post-command `PrepareAiHiring` pass derives the verified gate and adjustment
inputs, writes the next current role, and returns the prepared offer choice;
replay version 8 records that mutation.

The related 14-byte auxiliary records at `0x0048c0ba` remain deliberately
unmodeled. Their second short is initialized to the current sector by every
assigning hire-role-4/family-6 dispatch and is later updated by family-6 routing.
The first short is written as `-1` by several Equip/Heal/routing paths but as a
sector by Attack and other paths. Selector `0x5f` treats the second short as
coverage only when the first equals `-1`; otherwise it tests the gang's live
sector. This bounds the behavior but does not yet justify a single generic
“destination” name for either field.

**Next validation:** recover and represent the two per-gang auxiliary shorts so
family-6 coverage no longer relies on the current-sector/queued-Move semantic
projection.

### BIN-AI-003A - strategic hire-offer ranking

**Observation:** `0x00458fa0` calls `0x004078d9` after choosing a role for the
next hire. The helper scans exactly three offer bytes at `0x004abbc0 +
player * 3`, returns an offer-slot index, and only then compares that winner's
raw Force (`gang +0x00`, selector `0x8d`) with player cash. An unaffordable
winner returns `-1`; it does not fall back to another offer. If requested mode
0 has cash strictly above 200 and the scenario is not Greed, the helper first
substitutes mode 3.

| Requested mode | Ranking and eligibility |
|---:|---|
| 0 | Lowest Upkeep at or below 3, with nonnegative Control; later ties win |
| 1 | Highest Heal from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 2 | Highest Research from a zero baseline; Greed also requires Upkeep <= 3; later ties win |
| 3 | Highest Combat plus only positive Blade, Range, Fighting, and Martial Arts; zero baseline; later ties win |
| 4 | Highest Stealth + Strength. Greed requires Upkeep <= 4, Strength >= 0, and Stealth > 3 and gives later ties priority; other scenarios require Strength >= 0 and replace only on a strict improvement, so the first maximum wins |
| 5 | Highest Detect at or above the initial baseline 10; later ties win |

The field identities follow the decoded 156-byte gang record: Upkeep at +2,
Combat through Martial Arts at +4 through +30, and raw Force at +0. Direct
instruction inspection was required for modes 1, 2, and 4 because Ghidra's
decompiler reused the player parameter as a local accumulator and emitted
misleading pseudocode. `OriginalAiHireRules` implements the instruction-level
behavior, and the live AI planner uses the selected post-command role's ranking
mode.

When no offer survives ranking/affordability, selector `0x8e` chooses the slot
passed to `0x004078b8`, which writes `0xfe` into that offer's per-player state.
Greed always chooses slot zero (confirmed from the emitted instructions; the
decompiler renders its zero-iteration loop misleadingly). Every other scenario
chooses the first strict minimum of this integer score, initialized to 5000:

`Stealth * 20 * positive-stat-sum / (Force + Upkeep + 1)`

The sum includes positive Combat, Defense, Control, Heal, Influence, Research,
Strength, Blade, Range, Fighting, Martial Arts, and Tech; it excludes Stealth,
Detect, and Chaos. `OriginalAiHireRules.SelectRejectedOfferIndex` implements
this exact failure-path choice. The live post-command AI preparation returns
that choice when ranking or affordability fails, and the replay recorder applies
the snub as a separate authoritative mutation.

**Confidence:** Verified for all comparisons, eligibility boundaries, tie
directions, the cash-200 override, post-selection affordability, and no-fallback
behavior, plus the failed-hire rejection formula and tie direction.

**Next validation:** recover the scenario-specific role selection in
`0x00458fa0`, then use its chosen role to integrate this selector with live AI
hiring.

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

`0x0040a1a7` rebuilds a 24-byte record for each ordered player pair. For the
active observer, offset `+0` counts every sector owned by the other player.
Offset `+2` counts those sectors where the observer's gangs present there have
a strictly greater combined effective Combat + Defense total than the visible
defending owner's gangs. Selectors `0xb0`/`0xb1` enumerate only defenders whose
per-observer visibility byte is 1; selectors `0x5e`/`0x47` enumerate the
observer's own local gangs. The runtime gang bytes at `0x00498dba` and
`0x00498dbb` are effective Combat and Defense: the attack resolver independently
adds the former to Force at `0x00473dd3` and consumes the latter as Defense at
`0x00473b17`.

When both counts are positive, the exact test at `0x0040a816` is signed integer
`(advantaged sectors * 100) / owned sectors > 75`; exactly 75 percent does not
qualify. Target controller types 0 or 3 (local or legacy-remote human) enter the
test at Mentality 1 or higher. Other controller types enter it only below
Mentality 2. On success, `0x0040a859` sets the ordered pair's byte at `+20`, and
`0x0040a86d` writes `-10` to `attitude[observer, other]`. The row stride `0x90`,
pair stride `0x18`, and the same observer-major indexing in the flag's only
external consumer at `0x0042085d` establish the direction. The flag is an
ephemeral planner predicate; its consumer can admit a later branch when the
owner is already hostile and no visible defending gang is available.

The six family-handler calls form three paired gates. Four formerly anonymous
state selectors are now bounded:

- selector 3 returns the active player's cash. The turn resolver at
  `0x00472775` compares this field with action and equipment costs, subtracts
  those costs, and adds received income back into the same per-player word;
- selector 4 returns the selected sector's signed Tolerance byte at sector
  stride `0x24`. The right-panel presenter `0x004120ef` independently renders
  this byte on the `TOLERANCE` row;
- selector `0x21` returns the sector owner, or `-2` for a disabled sector; and
- selector `0x35` tests whether that owner has player type 0 or 3. Setup and
  initialization paths identify those types as local-human and legacy-remote
  human respectively, so the selector is a human-owner predicate. This is a
  planner classification only: original networking remains an explicit
  non-goal and no transport or protocol behavior is being recreated.

The raw planned-action bytes are also identified. The command atlas
`PX00129.bmp` lists the fourteen commands in numeric order, while the turn
resolver groups byte 10 by destination sector and accounts byte 3 using sector
income. Together these independently map byte 3 to **Chaos**, byte 10 to
**Move**, and byte 13 to **Snitch**.

The bounded family-1 blocks now establish several observable decisions. One
path keeps Snitch only when cash is greater than 50 and otherwise writes Move.
Another path requires at least 50 cash before entering its crime choice, then
writes Chaos when the target sector has Tolerance below 4 and Snitch otherwise;
its fallback writes Move. For a human-owned target, Mentality 1 or higher can
enter that crime choice. The paired non-human-owner path admits Mentality 0,
while another pair has an exact-Mentality-2 override. Mentality 3 shares the
`>= 1` branches but not that exact-value override.

The remaining selectors in those gates are now structurally identified:

- `0x3c` reads the gang's Force byte;
- `0x3d` reads its queued-action byte;
- `0x51` reads its effective Heal statistic; and
- `0x2c` is a strict single-gang Control feasibility predicate. It rejects
  disabled, unavailable, or already-owned sectors. For a neutral sector it
  tests whether gang Force + Control exceeds sector Income + Support. For an
  enemy sector it adds every defending gang's Force + Control to that defense
  and performs the same strict comparison. Its nested selector `0x91`
  enumerates other players' gangs in that sector only when the querying
  player's per-gang visibility byte is nonzero, so unseen defenders are not
  included in the AI estimate. The field at record offset `+23` is
  independently used by the Control resolver at `0x00472775`; offset `+24`
  returned by selector `0x51` is the following Heal statistic.

These are verified branch facts, not yet a complete policy table: the target
enumeration and earlier guards still determine which gang/sector pair reaches
each gate. Selector `0x3e`, which drives the handler's switch, is the
previous-turn action byte rather than a gang family. At the start of
`0x00458fa0`, each gang's three-byte action/target tuple at record offsets
`+7..+9` shifts to `+4..+6`, then the new tuple is cleared. Selector `0x3e`
reads offset `+4`; selector `0x3d` reads the newly planned action at `+7`.
The branches above are therefore command-continuity decisions, including the
case entered after a prior Snitch command.

All four action-7 (**Heal**) assignments in this handler are now bounded.
Every path first requires effective Heal at least `-3`; three require Force
below 9, while the path following no prior action or prior Chaos requires Force
below 8. No recovered family-1 path heals at Force 9. The recreation therefore
uses the conservative common boundary—Force below 9 and Heal at least `-3`—for
its provisional planner, while preserving the stricter history-specific gate
as pending continuity work.

**Interpretation:** `0x00487850` is the original match-global, zero-based AI
Mentality setting, seeded from a persisted preference and then carried through
setup staging/serialization. Family handler 1 uses it to redirect cash-qualified
crime behavior between human and non-human owners and between Chaos, Snitch,
and Move. A player-pair scoring pass also changes paths by mentality.

**Confidence:** Verified for resource IDs, address, display expression, query
selector, all six write classifications, all eight genuine consumer call sites,
cash/Tolerance/owner/human-owner selector meanings, command-byte mappings,
comparison constants, pair counters, integer ratio, observer-to-target write
direction, and resulting raw record writes; High for the global's identity,
persistence/setup flow, selector meanings, effective-stat labels, and the
bounded decisions above; Low for the complete planner policy and its target
enumeration.

**Next validation:** identify the sector/gang target enumerators and earlier
guards feeding each command-continuity gate. Then capture fixed-state
command-selection fixtures for the cash 50/51, Force 8/9, and Tolerance 3/4
boundaries before changing recreation policy.

### BIN-AI-005 - shared weighted sector selector

**Observation:** `0x00408642` is the shared sector-target routine used by the
recovered family handlers. Its parameters are the active player, a selection
mode, and the active gang. Mode 0 chooses one of the eight immediate neighbors
(`-9`, `-8`, `-7`, `-1`, `+1`, `+7`, `+8`, `+9`) with uniform calls to the
original bounded RNG, rejecting row-wrap and off-board results.

For nonzero modes the routine clears an 8-by-8 integer score map, obtains the
gang's current sector through selector `0x5a`, and examines successively larger
square rings around it, from radius 1 through 7, stopping after the first ring
which contributes any candidate. The current sector is removed before final
selection. Modes 1 through 5 have bounded scoring rules:

- mode 1 scores a neutral sector `+1` only when selector `0x2c` says the gang
  can take it by strict solo Control;
- mode 2 scores an owned sector `+1`;
- mode 3 scores a sector owned by another player `+1`;
- mode 4 scores `+1` when player-pair predicate `0x2d` accepts its owner; and
- mode 5 scores a solo-controllable neutral sector `+5`, an owned sector with
  no previous-Chaos gang assignment (selector `0x5b` equals zero) `+2`, and an
  enemy-owned sector `+1`.

The remaining modes are structurally bounded but not all subordinate fields
are named yet. Selector `0x32` counts players whose controller type is 0 or 3,
so mode 6 and mode 10 branch on the number of human players. Selector `0x2d`
compares two players' positions in the six-byte player-order table, rejecting
neutral and self comparisons. Selector `0x2e` returns the sole player whose
scenario standing byte at `0x004abc08` is zero, or `-1` when zero or multiple
players share that value. `0x0047712a` builds the scenario score at
`0x004a2790`, then sets each active player's standing byte to the number of
players with a strictly greater score and inactive slots to `0xff`; selector
`0x2e` therefore returns the unique current leader. Selector `0x5e` counts one
player's nonempty gang records in a sector.

Mode 6 is now bounded. If at least one human participates, a sector owned by a
player whom the active AI views negatively receives `+2` only when that owner
is human. It then adds one independent leader-routing point: with a unique
leader other than the active player, only that leader's sectors receive `+1`;
with no unique leader, every sector owned by a player tied at standing zero
receives `+1`; when the active player is the unique leader, every other
player-owned sector containing fewer than four active-player gangs receives
`+1`. These additions feed the same nearest-ring, maximum-tie RNG, and
orthogonal-step logic as the other nonzero modes.

The site-data offsets used by modes 7 through 9 align exactly with the decoded
62-byte `SITES` record: selectors `0x0c`, `0x0d`, and `0x10` return Support,
Cash, and Stealth for a sector's selected site slot. Selector `0x1c` tests
whether that site's definition Resistance minus its accumulated influence is
below one. Modes 7 and 8 score the Support and Cash of not-yet-influenced sites
in owned sectors. Mode 9 scores Stealth for already-influenced sites in owned
sectors. Mode 7 additionally requires selector `0x6f` to be zero; `0x6f`
counts a player's gangs in the sector whose immediately previous action at
planning-record offset `+5` is 9 (**Influence**). Before dispatch, the outer
planner also rewrites one prior-action byte when more than one gang retained
Influence in the same sector, preventing duplicate continuity assignments.

Mode 10 changes its owner test according to the human-player count. With no
human players it scores every non-neutral sector not owned by the active
player `+1`; with at least one human player it scores every human-owned sector
`+1`. This test does not consult the directional attitude table. Mode 11
gives `+1` to the sector returned by selector `0x5a` for the active player.
Modes 12 and 14 restrict selection to Big Man's central sectors 27, 28, 35,
and 36; modes 13 and 15 restrict it to Eliminate's six headquarters candidates
9, 12, 30, 33, 51, and 54. These four modes require a per-player path value
below 6 and award `+5` for a human-owned target versus `+1` otherwise, with
modes 12 and 13 also excluding sectors already owned by the active player.
Mode 16 gives `+1` to the sector returned by selector `0x77`; that selector
groups planning-family-11 records in blocks of six and returns the stored
anchor sector for the block containing the active gang. A mode above `0x3f`
directly adds `+1` to sector `mode - 0x40`.

All 48 direct references to `0x00408642` have also been enumerated. The static
mode arguments map to the recovered family handlers as follows:

| Selector mode | Handler/family call sites |
|---:|---|
| 2 | family 4 (`0x00401000`), four calls |
| 3 | family 9 (`0x004605e0`), two calls |
| 5 | family 0 (`0x00428ef0`), eight calls; family 1 (`0x00434080`), seven calls; family 7 (`0x00436c70`), one call |
| 6 | family 2 (`0x0041fef0`), two calls |
| 7 | family 5 (`0x0043a1d0`), four calls |
| 8 | family 3 (`0x00435bd0`), four calls |
| 9 | family 10 (`0x0042a6e0`), two calls; dispatcher `0x00432da0`, one call |
| 10 and 16 | family 11 (`0x00420950`), two mode-10 calls and one mode-16 call |
| 12 and 13 | family 13 (`0x0040abc0`), one call each |
| 14 and 15 | family 14 (`0x00466910`), one call each |

The one mode-0 call belongs to runtime function `0x00476a94`, outside the
family table. Family 6 (`0x00431c60`) dynamically uses mode 2 when selector
`0x60` finds no stored target and otherwise encodes that target as
`sector + 0x40`. Family 7 and family 12 also contain encoded-sector calls.
No direct call carries literal mode 1, 4, or 11; any live use must therefore
come through a computed argument. `tools/ghidra/ReportCallArguments.java`
provides a repeatable bounded inventory of the three pushed arguments at each
direct call.

Both mode-6 calls belong to family 2. That handler writes public action byte 10
(**Move**) with the selected mode-6 destination when its current/selected
sector branch cannot proceed locally. Its visible-gang path writes action byte
1 (**Attack**). At the later local-sector decision, the pair flag from
`0x0040a1a7` supplies one of two exact action-byte-4 (**Control**) gates: the
sector owner must already be viewed negatively, no defending owner gang may be
visible, and the observer-to-owner combat-advantage flag must be set. The other
Control gate is restricted to a hostile human-owned sector with zero visible
human gangs and rejects a previous-turn Control action. Thus the pair flag does
not directly select an attack; it permits Control after the territorial
Combat + Defense test has established overwhelming local advantage.

The surrounding action writes give the site modes public command semantics.
Family 5 uses mode 7 after writing Move; when the selected Support-priority
site is already local, the same branches write Influence instead. Family 3
has the identical shape around mode 8 but ranks sites by Cash. These are thus
Support-focused and Cash-focused Influence strategies, not distinct movement
rules. Family 10 calls mode 9, compares selector 8 for the chosen and current
sectors, and moves only when the chosen sector has the larger value. Selector
8 sums Stealth across completed/influenced sites, so this path seeks a stronger
local Stealth modifier before its Hide-or-Chaos decision. The dispatcher also
contains a scenario-specific mode-9 Move for gang slot zero.

Families 13 and 14 both use the fixed objective sets as Move destinations:
scenario value 8 selects modes 12/14 and is Big Man, while scenario value 6
selects modes 13/15 and is Eliminate. Family 13 uses the variants that exclude
already-owned objectives; family 14 uses the variants that retain them. This
establishes an original Eliminate movement bias toward every possible
headquarters location, not merely an attack-score bonus against a currently
visible Right Hands gang.

`0x00408553` sorts the 64 sector scores descending while retaining their sector
indices. The caller chooses uniformly among every sector tied for the maximum.
If that strategic target is outside the immediate 3-by-3 neighborhood, the
routine returns one orthogonal step toward it rather than the distant target,
and refuses an axis step whose per-player path value exceeds 5. If the selected
target is already adjacent, the sector itself is returned. Candidate sectors
are also removed late when marked unavailable or when the active gang cannot
strictly Control a non-owned destination under the relevant gang-state branch.

**Interpretation:** mode 5 is the general movement fallback recovered in the
family-1 continuity paths. Its exact neutral/owned/enemy ratio is 5:2:1, and
the routine separates strategic target scoring from the single-tile Move that
is ultimately queued. Randomness is used only for mode-0 neighbor selection
and equal-best final scores in the bounded paths inspected here.

**Confidence:** High for the address, ring expansion, score-map sorting,
mode-0 directions, modes 1 through 5 weights, site-field offsets, human-player
count, gang-in-sector count, unique-leader selector, mode-6 weights and owner
branches, fixed sector sets, direct call inventory, maximum-score tie
randomization, and orthogonal next-step return. Medium for the player-order
predicates, family-11 anchor, dynamic call arguments, and late candidate
filtering due to decompiler control-flow folding. Mode 10 and mode 16's
remaining family-11 guards are not yet fully labeled.

**Next validation:** map the remaining family-11 guards for modes 10 and 16 to
public commands. After that, reproduce the 5:2:1 mode-5 target score, mode-6
leader/hostility weights, path threshold, and
equal-best RNG with fixed-state reference traces before replacing the
recreation's provisional destination weights.

### BIN-AI-006 - directional attitude and hostility matrix

**Observation:** `0x004ab590` is a six-by-six signed integer matrix indexed as
`observer * 6 + other player`. Initialization at `0x0046dc10` depends on the
global AI Mentality. At Homicidal Maniac, every cell whose target player has
human controller type 0 or 3 is initialized to `-10`, while cells targeting a
computer player are initialized to `+10`. At all lower mentalities every cell
starts at zero. This changes preferences only; it grants no resources,
statistics, rolls, or visibility.

At the start of turn resolution at Mentalities 0 through 2, every entry below
`+10` increases by one. Homicidal Maniac skips the entire recovery loop.
Later resolution paths subtract from one directed cell and clamp the result at
`-10`. At non-Homicidal mentalities, initialization gives every player a fixed
reaction value from one bounded RNG draw, `Next(4) + 2`, producing 3 through 6.
Homicidal Maniac assigns reaction zero and consumes no such draw. No later
writer modifies these values. The combat path lowers the defender owner's
attitude toward the attacker by `max(reaction, damage dealt)`. A sector-control
transfer lowers the previous owner's attitude toward the new owner by exactly
twice that previous owner's reaction value.
The player-pair ratio pass at `0x0040a1a7` can also force a directed entry to
`-10` under its mentality-dependent guards.

Negative entries are the hostility boundary used by central target queries.
Selector `0x92` enumerates visible gangs in a requested sector only from
players whose observer-relative entry is negative; selector `0xab` performs a
corresponding count for visibility state 1. Selector `0x90` returns weight 10
for a visible human gang belonging to a negatively viewed player and weight 1
for other visible gangs. Numerous AI handlers read the same `< 0` predicate
directly, including shared sector-selector mode 6.

Family 11 (`0x00420950`) supplies the clearest consumer. Instruction-level
inspection is required here because the decompiler drops the assignments after
the entry calls: selector `0x5a` is saved at stack local `-0x4` and is the
active gang's current sector; selector `0x61` is independently saved at `-0x8`.
Selector `0x61` chooses a weapon upgrade. Its subordinate selector `0x6d`
admits only a requested weapon class whose tech requirement does not exceed
selector `0x62`'s local research ceiling, whose player research flag is clear
(complete), and whose cost does not exceed current cash. It compares the first
eligible item Combat
bonus for the melee, blade, and ranged classes after adding the matching
effective skills: Strength for melee, Strength + Blade for blade, Range for
ranged, and Strength + Fighting + Martial Arts for bare hands. It then scans
all 64 items in the winning class and retains only a strictly larger Combat
bonus subject to the same research and cash gates, but this second pass compares
item Tech against the gang's raw Tech rather than selector `0x62`. Class-score
ties resolve ranged, then melee, then blade, then bare hands. It returns `-1` when
bare hands win, no upgrade beats the baseline, or the result is already the
equipped weapon.

Selector `0x65` reads the first of two planning-record shorts at offsets
`+12/+14`. Planning initialization sets the first to zero for an unarmed gang
and otherwise decrements it by one; a weapon Equip writes `item cost * 3` back
to it. Family 11 admits the selector-`0x61` weapon only when this replacement
cooldown is at most zero, the item is affordable, and the previous action is
not **Attack**. It then writes **Equip**, the item ID, and the new three-times-
cost cooldown. The following analogous opportunities use selectors `0x64` and
`0x74` for the other equipment slots before the Heal gate.

After those Equip and Heal opportunities, the handler reads the owner of its
current sector. In an active-player-owned sector it always writes **Move** and
calls mode 10.

In any other sector, selector `0xac(active player, current sector, 0)` scans
other players in ascending slot order and their gangs in ascending slot order.
It returns the first encoded `player * 81 + gang` whose sector equals the
current sector, whose observer-specific visibility/status byte is nonzero, and
whose raw gang-state byte at record offset `-1` from the sector field is zero.
The handler writes **Attack** against that decoded player/gang whenever the
result is nonnegative. If no such target exists, selector `0x76` determines
formation leadership: considering only family-11 gangs in ascending gang-slot
order, ordinals 0, 6, 12, and so on return 1. Those anchors write **Move** with
mode 10 and replace their stored formation-sector short with the chosen
destination. Other family-11 gangs write **Move** with mode 16 and retain their
current-sector short. Selector `0x77` finds the corresponding block anchor and
returns its stored formation-sector short, so mode 16 awards that sector `+1`
and feeds it through the common ring/path selection.

Thus mode 10 is not a generic hostile-target rule: with humans present it seeks
human-owned territory regardless of attitude, and with no humans it seeks any
other non-neutral owner's territory. Mode 16 is the follower path for five of
each six family-11 gangs, while the first gang in each block establishes the
formation destination.

**Interpretation:** this is an attitude/hostility system, not a scalar combat
bonus. Homicidal Maniac begins maximally hostile toward human players and
maximally friendly toward computer players; ordinary interactions can create
directed hostility at other mentalities, and hostility decays toward
friendliness by one point per turn. The recreation's current attack score
approximates part of the visible outcome but does not yet persist or resolve
this matrix.

**Confidence:** High for matrix dimensions and direction, `[-10,+10]` bounds,
initial values, mentality-gated per-turn recovery, reaction range/immutability, combat and
Control decrements, negative-hostility target gating, controller classification,
and mode-10 target ownership. Medium for mode-16 group semantics. Low only for
the original public/internal name of the reaction value.

The exact new-match order is now bounded. For non-Homicidal games the six
reaction draws are the only RNG calls between entry to `0x0046dc10` and city
generation at its decompiled line 85. Homicidal games skip those draws. The
offer filler remains later in the outer setup caller. The recreation now
initializes at this point, recovers attitudes at the Command-to-Execution
whole-turn resolver boundary, applies the recovered combat and Control changes,
uses hostility for AI attack candidates, and includes the state in canonical
hashes, native saves, and replays.

**Next validation:** capture fixed original traces proving the combat-advantage
threshold, reaction, recovery ordering, and family-11 weapon replacement
cooldown through the first complete turns.

### BIN-AI-007 - per-player difficulty resolution band

**Observation:** new-match initialization also fills a six-entry integer table
at `0x004a2570`. Every slot starts at 1. At Goon, computer-controlled slots are
changed to 0; Criminal leaves all slots at 1; Crime Lord and Homicidal Maniac
change computer-controlled slots to 2. Human-controlled slots remain 1 at all
four mentalities.

The whole-turn resolver `0x00472775` reads this table repeatedly while resolving
gang actions. Its helper `0x00475f70(pool, threshold)` rolls `pool` inclusive
d6 values with `0x0045d227(6)` and counts results greater than or equal to the
threshold. Every one of the nine band-table reads is now bounded:

- Heal uses `Heal + 4` dice. Bands 0/1 succeed on 5+, while band 2 succeeds on
  4+; successes add Force, capped at 10.
- Influence uses `Force + Influence`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 5+; band 1 uses the full pool at 5+; band 2 uses the full pool at
  4+.
- Research uses `Force + Research`. Band 0 removes `trunc(pool/5)` dice and
  succeeds on 6; band 1 uses the full pool at 6; band 2 uses the full pool at
  5+.
- Each Chaos gang separately includes sector Income in
  `Income + Force + Chaos`. Band 0 removes one fifth of the pool at 5+, band 1
  uses the full pool at 5+, and band 2 uses the full pool at 4+. When the band-2
  player owns that sector, only `successes - trunc(successes/4)` contributes to
  the Crackdown comparison.
- A hidden target evades when an inclusive d20 roll is below
  `Stealth + 14 - Detect` for attacker bands 0/1 or
  `Stealth + 10 - Detect` for band 2.
- Main Attack reduces a band-0 defender's Defense by one quarter. It then rolls
  `Force + CombatRating - adjusted Defense` at 6+/5+/4+ for attacker bands
  0/1/2. Positive pools impose minimum damage `trunc(pool/4)`.
- Retaliation first requires the target's action byte not to be 8 (**Hide**).
  It is then allowed when the attacker has effective Martial Arts zero, when
  the attacker has a weapon equipped, or when the defender has both positive
  effective Martial Arts and no weapon equipped. Equivalently, a bare-handed
  positive-Martial-Arts attacker suppresses retaliation unless the defender is
  also a bare-handed positive-Martial-Arts gang. Eligible retaliation rolls
  its corresponding pool at 5+ for defender bands 0/1 or 4+ for band 2, then
  halves successes using integer truncation.

The recreation implements these bands and formulas in
`OriginalResolutionRules` and routes the corresponding resolution paths
through them. This is a mechanical resolution calibration, not merely a
planning preference.

The compound operands are direct gang-record fields. Offset `+7` is the public
action byte; selector `0x39` exposes offset `+4`, the equipped-weapon item or
`-1`; and selector `0x58` exposes offset `+31`, the last of the fourteen
effective statistics and therefore Martial Arts. The attack block uses those
same offsets from both its copied attacker record and the targeted live record.

**Confidence:** High for initialization, controller/mentality mapping, helper
semantics, all nine reads, formulas, thresholds, integer truncation, and the
complete retaliation-eligibility predicate.

**Next validation:** capture fixed original traces at bands 0, 1, and 2 for
every affected action, including Hide and both Martial Arts branches.

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

**Observation:** `0x0046e766` initializes each player's three fixed offer bytes
at `0x004abbc0` to signed -100 and the matching action bytes at `0x004a27c8` to
-1. `0x004078b8` writes snub action -2 to the selected slot. The resolver at
`0x00472775` scans players 0..5 and slots 0..2, negates that same slot's offer
for a snub or successful hire, clears its action, and creates a hired gang with
a bounded `1..5` result plus four. Failed hires clear the action without
negating the offer. The planning-entry helper `0x004716eb` later scans slots
0..2 and fills each negative slot in place using repeated bounded `1..89` draws,
rejecting all currently positive slot values and that slot's negated old ID.
Its only callers are the computer and human planning-entry paths at `0x0046f4e3`
and `0x0046fe8b`; the resolver does not call it. The human handler at
`0x00416c75` assigns the selected slot and clears both other action bytes, so
hire and snub are mutually exclusive selections rather than two actions in one
turn. Its Reject branch toggles the selected slot from -1 to -2 or from -2 to
-1; clicking Reject on a slot holding a sector destination changes it to -1
rather than directly snubbing it. Dragging an offer writes the new destination
to that slot and unconditionally clears the other two slots, so a later drag
replaces the earlier selection and dragging the same offer can retarget it.
The handler does not read or update cash or cumulative cash spent. The resolver
first counts all gang records in the target sector at `0x0047592b`-
`0x004759a8`; six causes a failure with no random draw. It then checks
then-current cash at `0x004759bd`-`0x00475a15`. Only after those checks does it
roll Force at `0x00475ac4`; it subsequently searches the player's 80 gang slots
at `0x00475bdb`-`0x00475c2d`, so a full roster failure consumes the Force draw.
After successful gang creation, it updates cumulative cash spent at
`0x00475c88`-`0x00475ca8` and subtracts cash at
`0x00475cba`-`0x00475ce4`. Rejected, cancelled, and failed hires do not change
either value.

**Interpretation:** Initial offers are populated on planning entry. Successful
hire or snub leaves a signed tombstone in its permanent slot until that player's
next planning entry; refill never compacts or shifts slots. Multiple malformed
vacancies would refill in ascending slot order. Replacement selection is
rejection sampling over gang IDs 1 through 89; a just-hired or snubbed gang
cannot immediately replace itself. Hired Force is uniformly 5 through 9 through
the recovered bounded wrapper. Selecting a hire reserves no cash: affordability
is evaluated during resolution, and an unaffordable action is cleared without
creating a vacancy.

**Confidence:** High static evidence for arrays, sentinels, selected-slot writes,
mutual exclusion, resolver/refill order, call sites, and RNG bounds; a controlled
runtime sequence remains useful corroboration.

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

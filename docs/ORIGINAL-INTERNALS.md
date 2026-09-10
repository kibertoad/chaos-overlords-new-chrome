# Original executable internals research

Status: active clean-room research log
Last updated: 2026-09-10
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

### BIN-AI-003C - AI hire destination and persistent placement anchor

**Observation:** The hire-destination helper `0x00408214` takes player, mode,
and offer slot. Values at least `0x40` directly write and return sector
`mode - 0x40`, without validation or RNG. Mode 1 builds a weighted multiset in
exact order: every owned sector from 0 through 63 once, followed by every
active gang slot from 0 through 80 once at that gang's current sector.
Duplicate sectors remain duplicated, and one bounded draw selects a one-based
ordinal. An empty multiset still calls the original wrapper with its bound
clamped to one and resolves destination `-1`.

Mode 0 first scans occupied gang records to find the minimum and maximum sector
and each extreme's multiplicity. When an offer slot is present and either
extreme contains fewer than six gangs, it consumes one bounded draw over two
choices, tentatively picks that extreme, and falls back to the other when the
chosen extreme is full. Execution then deliberately falls through mode 1 and
overwrites the tentative result. Thus the extreme choice changes RNG state but
not the final destination. If both extremes are full or the offer slot is
negative, that preliminary draw is skipped. Modes 2 through 63 return sentinel
99 without writing a destination or consuming RNG.

The outer planner `0x00458fa0` maintains one per-player encoded placement anchor
at `DAT_0048e2f8`. New-match initialization seeds it to the Right Hands sector
plus `0x40`. It retains a proposed anchor only while that owned sector has at
least one neutral, available immediate neighbor, occupancy at most five, and
the scenario is not Big Man; otherwise selector `0x25` chooses a replacement,
which is stored plus `0x40`. The anchor is part of the original save/load state.

Selector `0x24(player, center)` returns zero unless `center` is player-owned,
then counts neutral cells whose Crackdown-duration byte is zero in its 3-by-3
neighborhood in dy-major, dx-minor order. Its row-wrap check uses the literal linear bound
`0 <= candidate < 65`. Index 64 is not a fixed sentinel: its owner and
Crackdown-offset reads alias bytes at `0x004a11e8` and `0x004a11f7` in the
following 486-by-10-byte runtime block. Bounded decompilation of the resolver at
`0x00472775` shows that byte zero of record zero is the mirrored owner for
player-zero gang slot zero. The index-64 availability read aliases byte five of
record one, a retaliation-damage accumulator for player-zero gang slot one.
Fresh recreation matches initialize the owner alias to player zero, so the
neutral-neighbor predicate never consults the availability alias at index 64.
Arbitrary save-loaded alias values are irrelevant because original saves are
unsupported.
For ordinary scenarios selector `0x25` makes three deterministic ascending-
sector passes over owned sectors with occupancy below six:

1. choose the first strict maximum positive selector-`0x24` count (baseline
   zero, so zero never qualifies and equal values retain the earlier sector);
2. if none, choose the first sector whose selector-`0x5b` previous-Chaos count
   is zero; and
3. if none, choose the first strict minimum nonzero selector-`0x26` count from
   baseline nine. Selector `0x26` counts non-player-owned cells in the same
   literal 3-by-3 bounds and does not test availability.

If all three passes fail, selector `0x25` returns `-1`. Big Man (scenario 8)
instead has an empty radius-zero list, then tests `[18, 26, 19, 27]`, then
`[9, 17, 25, 33, 10, 18, 26, 34, 11, 19, 27, 35, 12, 20, 28, 36]`, taking the
first player-owned sector with occupancy below six. If neither list succeeds,
it preserves the incoming anchor even when that anchor is full. Selector
`0x25` consumes no RNG.

The normal failure value `-1` is stored as encoded anchor 63. A later refresh
subtracts 64 passes raw `-1` to selector `0x24`, whose center-owner read occurs
before its neighborhood bounds checks and therefore aliases adjacent memory as
well. Big Man is the only selector-`0x25` path that reads the incoming anchor;
when its scans fail it returns `anchor - 64`, so the outer re-encoding preserves
63, ordinary sector encodings, or the inactive encoding 164 unchanged.

Normal planner calls to `0x00408214` pass this encoded anchor, so they always
take the direct `>= 0x40` path and consume no placement RNG. The role-4 paths
for Kill 'Em All, Power, Greed, Big 40, Acceptance, Dominance, and Armageddon
(scenario IDs 0 through 5 and 9) can instead override the anchor with the first
visible-hostile sector returned by selector `0x9a`, again encoded with
`0x40`. Siege hire-role slots 5 and 7 override it with the Right Hands sector
plus `0x40`. Resolution at `0x00472775` later consumes the selected destination
during the internal Hire phase.

The ten normal `0x00408214` call sites are `0x00459bc8`, `0x0045a08c`,
`0x0045a55c`, `0x0045aafe`, `0x0045af7e`, `0x0045b3fe`, `0x0045b671`,
`0x0045ba56`, `0x0045bc23`, and `0x0045bfe9`. The seven visible-hostile
overrides occur at `0x00459bb1`, `0x0045a075`, `0x0045a545`, `0x0045aae7`,
`0x0045af67`, `0x0045b3e7`, and `0x0045bfd2`; Siege's Right Hands override is
at `0x0045ba3f`. All 18 direct xrefs are inside the outer planner.

**Interpretation:** ordinary AI hiring does not use mode 0 or mode 1's random
placement result: the planner has already reduced the choice to a persisted,
encoded sector. The otherwise surprising mode-0 draw is relevant only if an
unencoded caller reaches that helper.

**Confidence:** High static evidence for `0x00408214` mode control flow, exact
mode-1 multiset order and RNG use, encoded direct mode, anchor initialization
and persistence, selectors `0x24` through `0x26`, all direct call sites,
scenario overrides, and the planner/resolver call path. The behavior is
recovered and wired into the recreation's live AI planner.

**Implementation:** the six encoded anchors are authoritative `AiPlanningState`
members covered by canonical hashes, native saves, and replays. Fresh local
matches create all six original player slots and initialize each anchor from
gang slot zero. `OriginalAiHirePlacementModeRules` isolates the exact transient
role/scenario override, including the raw-100 visible-hostile sentinel. Live AI
preparation now preserves or refreshes the anchor, counts previous Chaos actions
at the candidate sector, selects the first visible hostile regardless of its
controller type, applies Big Man and Siege overrides, and feeds the encoded
zero-RNG result to deferred Hire resolution.

**Implementation:** `AiPlanningState` now persists and hashes the older,
immediately previous, and newly planned action bytes for all six-by-81 slots.
Active slots roll at AI planning entry; accepted computer commands update the
planned byte, and cancellation clears it. Hire resolution now reuses the first
inactive roster slot and clears that slot's family and three action generations.
The six first-plan flags are now authoritative, hashed, and persisted: first
preparation resets all 81 records and skips rollover, while subsequent passes
roll active records. The recovered duplicate cleanup is live: for each sector
with more than one previous Chaos, selector `0x70(..., 1)` rewrites only the
first ascending matching slot to None; it then does the same for previous
Influence through selector `0x71`, rewriting the first match to Snitch. The two
command-dependent target bytes now roll, reset, hash, save, and replay with
their action generation. Accepted computer commands encode them using the
resolver-confirmed meanings documented in `BIN-AI-004`.

**Next validation:** capture fixed original placement decisions across ordinary,
Big Man, visible-hostile, and Siege paths while preserving pass order and the
zero-RNG encoded path. Variation caused only by arbitrary original-save alias
bytes is outside the supported scope.

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
- `0x2a` tests the current sector's Crackdown record and returns nonzero while
  police are active; and
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
each gate. The AI planning records begin at `0x0048a250`, use a 16-byte stride,
and retain three three-byte action/target generations: older at offsets
`+2..+4`, immediately previous at `+5..+7`, and newly planned at `+8..+10`.
Selectors `0x3f`, `0x3e`, and `0x3d` read their action bytes at `+2`, `+5`,
and `+8` respectively. The raw action values 0 through 14 are the public
command IDs; this is planner history, not a separate internal action enum.

For each active slot, `0x00458fa0` performs the exact rollover before planning:
`+2..+4 <- +5..+7` at `0x004590a9`/`0x004590c2`, then
`+5..+7 <- +8..+10` at `0x0045913f`/`0x00459158`, followed by clears of the
new tuple at `0x004591d5`, `0x004591ef`, and `0x00459209`. Strategic refresh
then runs at `0x0045936f`. The duplicate-Chaos cleanup queries selector `0x5b`
at `0x0045939c` and may rewrite the immediately previous action at
`0x004593d6`/`0x00459424`; only afterward does the dispatcher visit all active
gangs at `0x004594df` and write their new actions at `+8`. The hire-anchor
selector-`0x24` check at `0x00459502` and selector-`0x25` fallback at
`0x00459553` therefore observe the `+5` immediately previous action. There is
no second promotion at the end of planning.

Selector `0x5b`'s dispatcher case at `0x004048b8` counts same-sector roster
members whose `+5` action is 3 (**Chaos**); selector `0x6f` at `0x00404949`
does the analogous count for previous action 9 (**Influence**). Inactive
records use sector 100. Their tuples are not shifted or cleared by rollover,
but the dispatcher resets a slot when it is reused, and selector `0x5b` omits
inactive slots naturally through its same-sector test.

Reset/initialization helper `0x00409de1` clears offsets `+2..+10`, family 99,
and the other per-record planning fields. The complete six-by-81-by-16-byte
record block (length `0x1e60`) and the six first-plan flags at `0x00482108`
are both serialized by save/load paths `0x0046381a` and `0x00463cc5`. Thus the
history survives save/load independently of the command resolver and has an
explicit first-planning lifecycle.

Hire resolution in `0x00472775` scans gang records upward from slot zero while
the mirrored sector byte is not 100, with a strict usable bound of 80. When it
finds the first inactive slot it copies the complete new 32-byte gang record
into that slot; a full first 80 slots takes the failure path. This establishes
ascending inactive-slot reuse, not append-only roster growth. The planner's
reused-record reset prevents the new gang from inheriting the prior occupant's
family or action history.

The branches above are therefore command-continuity decisions, including the
case entered after a prior Snitch command.

All four action-7 (**Heal**) assignments in this handler are now bounded.
Every path first requires effective Heal at least `-3`; three require Force
below 9, while the path following no prior action or prior Chaos requires Force
below 8. No recovered family-1 path heals at Force 9. For that previous-None or
previous-Chaos path, the complete terminal branch is recovered: below the
strict Force/Heal boundary it writes Heal when selector `0x2a` reports no
Crackdown and Move through mode 5 when police are active; outside that boundary
an older Snitch writes Chaos and every other older action writes mode-5 Move.
The recreation executes this action-level branch through
`OriginalAiFamilyOneRules`. Replay-recorded preparation now supplies its exact
mode-5 destination and tie RNG, including the all-zero post-filter path. Only
the fallback when the selected action/step has no legal recreation candidate
remains provisional.

The other family-1 paths use the common Force-below-9 Heal gate. The isolated
rules also guard two distinct cash comparisons: the strict continuation changes
from Move at cash 50 to Snitch at 51, while the post-equipment crime branch
admits cash 50 and changes from Chaos to Snitch as Tolerance moves from 3 to 4.

The post-equipment continuation at `0x004345d0..0x00434712`, reached after
prior Control, Equip, or Snitch when no preceding equipment opportunity writes
Equip, is now complete. Selector `0x35` splits on whether the current sector
owner is human. A human owner takes the crime branch at cash at least 50 and
Mentality at least Criminal. A non-human owner takes it only when the raw owner
differs from the active player, is strictly greater than zero, cash is at least
50, and Mentality is exactly Goon. The literal positive-owner comparison means
player zero is deliberately excluded on this side. The crime branch writes
Chaos at Tolerance at most 3 and Snitch from 4; every failed gate writes Move
through mode 5. `OriginalAiFamilyOneRules.SelectPostEquipmentContinuation`
preserves these comparisons in the live planner.

The preceding equipment decision is also recovered and live. Selectors `0x39`
and `0x3a` read the equipped weapon and armor. Selector `0x61` chooses the
weapon candidate first. Selector `0x64` then chooses a researched type-3 armor
candidate whose Tech requirement is within the gang's raw Tech, whose Defense
at item-record offset `+0xc` strictly improves on the current armor, and whose
cost is strictly less than cash. Selectors `0x65` and `0x66` read signed
planning-record cooldown shorts at offsets `+12` and `+14`. A successful Equip
writes raw item cost times three to the matching cooldown. At the start of each
later planning pass, an equipped slot decrements its cooldown while an empty or
inactive slot resets it to zero.

Selector `0x6c` scans the acting gang's 3-by-3 neighborhood, clipping horizontal
wrap but permitting linear index 64. In Greed, a nearby cell qualifies when its
cached weight is 10 and its owner is the acting player. In other scenarios, a
cell qualifies when it has any different nonnegative owner or its cached weight
is 10. Such a cell opens the gate only while the gang lacks a weapon or armor.
Independently, a current sector owned by the acting player with weight 10 opens
the gate even when both equipment slots are filled. Weight 10 denotes a visible
hostile human gang. Index 64 preserves the original array aliases: its owner
reads player zero gang-slot-zero ownership storage, and its per-player weight
reads the next player's sector-zero weight (zero for the final player). The
recreation carries these comparisons, exact item target, cooldowns, and the
fallback continuation through authoritative planning, command resolution,
saves, canonical hashes, and replays.

The previous-Heal case is also complete at the action level. It repeats Heal
under the common Force-below-9/effective-Heal-at-least-`-3` gate. Otherwise it
queries selector `0x2c` for the acting gang's current sector, writes Control
when that strict solo-control predicate succeeds, and writes Move with a mode-5
destination when it fails. `OriginalAiFamilyOneRules.SelectHealContinuation`
and the live planner preserve this branch. Its complete mode-5 target selection
is live; only the recreation fallback when the selected command is unavailable
remains provisional.

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
persistence/setup flow, selector meanings, effective-stat labels, the complete
three-generation action-history lifecycle and serialization, and the bounded
decisions above; Low for the complete planner policy and its target enumeration.

The resolver at `0x00472775` establishes the complete public-command decoding
of those two bytes. Attack uses target player and that player's roster slot.
Equip and Research use an item ID in byte one. Influence uses the local site
slot in byte one. Move uses the destination sector. Give uses an equipment-slot
mask (`1` weapon, `2` armor, `4` miscellaneous) followed by the friendly target
roster slot. Sell uses the same mask in byte one. Commands without an explicit
target leave both bytes zero. Direct family-handler writes independently
confirm the Move, Equip, Attack, Influence, and Research cases; resolver lines
151-207, 346-352, 557-616, and 713-715 provide bounded decode evidence.

**Next validation:** identify the remaining sector/gang target enumerators and
earlier guards feeding each command-continuity gate. The disassembly-derived
cash 50/51, Force 8/9, effective-Heal -3/-4, Crackdown on/off, and Tolerance 3/4
vectors are executable regression tests. Capture controlled original-turn
decisions for the still-isolated branches and corroborate the two live
continuation branches before replacing more recreation policy.

### BIN-AI-005 - shared weighted sector selector

**Observation:** `0x00408642` is the shared sector-target routine used by the
recovered family handlers. Its parameters are the active player, a selection
mode, and the active gang. Mode 0 chooses one of the eight immediate neighbors
(`-9`, `-8`, `-7`, `-1`, `+1`, `+7`, `+8`, `+9`) with uniform calls to the
original bounded RNG, rejecting row-wrap and off-board results.

For nonzero modes the routine clears an 8-by-8 integer score map, obtains the
gang's current sector through selector `0x5a`, and examines successively larger
clipped squares around it, from radius 1 through 7. Each radius rescans the full
square rather than only its perimeter, and the search stops after the first
square which contributes any candidate. The current sector is removed before
final selection. Modes 1 through 5 have bounded scoring rules:

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
`+1`. These additions feed the same nearest-square, maximum-tie RNG, and
x-then-y step logic as the other nonzero modes.

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
9, 12, 30, 33, 51, and 54. These four modes require the active player's gang
count in that sector to be below 6. Modes 12 and 13 also exclude sectors already
owned by the active player. Instruction-level inspection at
`0x004093d3..0x004099ad` corrects the earlier shorthand about their weight: a
hostile human-owned objective receives `+5` inside the mode case and is then
multiplied by five again in the common post-switch block, for an effective
weight of 25. Every other admitted objective receives `+1`.
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

Focused inspection of family 3 at `0x00435bd0` now bounds its cash-site
continuations. Immediately previous actions None, Control, Equip, and Heal
share one branch. It first writes Heal only below Force 8 with effective Heal
at least `-3`. Otherwise, in a sector owned by the acting player, it scans the
three sites in slot order and retains the first strict maximum positive Cash
whose remaining Resistance is positive, writing Influence with that local site
slot. With no qualifying site it writes Control when selector `0x2c` accepts
the current sector, and otherwise Move through mode 8. Previous Snitch writes
that mode-8 Move directly.

Previous Influence first runs the same selector-`0x6c` equipment opportunity
used by family 1, including weapon-before-armor selection and the cost-times-
three cooldown. Without Equip it applies the same Force-8/Heal-`-3` gate. It
then retains the previous Influence site while that site remains unfinished
and the sector remains owned; otherwise it rescans for the first strict
maximum positive-Cash unfinished site, or moves through mode 8 when none
exists. The mode-8 selector block independently confirms that each owned
candidate sector is scored by the sum of every positive Cash value among its
unfinished sites, before the common nearest-square, maximum-tie RNG, and
x-then-y routing logic.

Previous Attack, Hide, and Move share an opponent-continuation branch. When the
cached visible-opponent weight is not 10, it returns to the owned cash-site,
strict Control, or mode-8 Move sequence without the earlier Heal gate. At
weight 10 it makes one bounded draw. Hostile-owned sectors draw an actual
target from visible gangs belonging to human-controller players; other sectors
draw from every visible opponent. Both pools scan player-major, then gang-slot
order.

The comparison selector preserves a notable original asymmetry: it receives
the selected ordinal but resolves that ordinal through the every-visible-
opponent pool even when the actual target came from the human-only pool. It
permits Attack when
`(target Force + target Combat) / 4 - attacker Defense` is no greater than
`attacker Force + attacker Combat - target Defense`; failure leaves None and
clears the auxiliary target fields. A successful comparison attacks the actual
selected player/slot tuple, not necessarily the gang used for the comparison.

After the switch, three consecutive Move actions change the stored family to
11 in Siege and 2 in every other scenario. Finally, with fewer than four turns
remaining in Greed, the handler unconditionally overwrites the planned action
with Terminate. Switch cases without a recovered action body intentionally
leave None rather than invoking a generic fallback.

**Recreation status:** the complete family-3 handler is live and replay-
recorded, including local site targets, equipment cooldowns, mode-8 Move
targets, opponent-pool selection and comparison, terminal family transitions,
and the late Greed Terminate override.

**Confidence:** High for the switch cases, comparisons, action writes, pool and
site scan order, tie behavior, mode-8 score sum, and call order from the
fingerprinted version-1.1 executable; runtime corroboration remains pending.

Focused inspection of family 5 at `0x0043a1d0` establishes that it has the
same complete action-switch and terminal shape as family 3, with Support-site
selection and mode 7 substituted for Cash-site selection and mode 8. Previous
None, Control, Equip, and Heal use the same Force-below-8 and effective-Heal-
at-least-`-3` gate, then select the first strict maximum positive-Support
unfinished site in an owned sector, attempt strict solo Control, or Move.
Previous Influence uses the same weapon-before-armor equipment opportunity and
cost-times-three cooldowns, Heal gate, previous-site retention, and local site
rescan. Previous Snitch moves directly through mode 7.

The previous Attack/Hide/Move branch is also instruction-for-instruction
equivalent in public behavior: visible-opponent weight 10 performs one bounded
draw, optionally chooses the actual target from visible human-controller gangs,
resolves the comparison ordinal through the full visible-opponent pool, and
uses the same quarter-strength combat predicate before writing Attack or None.
Its terminal block applies the same three-consecutive-Move family change (11 in
Siege, 2 otherwise) and the same final-three-turn Greed Terminate overwrite.

Mode 7 adds one distinction beyond its Support score. Selector `0x6f` scans all
81 planning records for the acting player and counts records whose mirrored
gang sector matches the candidate and whose immediately previous action at
offset `+5` is Influence. A candidate owned sector is scored only when that
count is zero; its score is then the sum of positive Support values for all
unfinished sites. Selector `0x41` reads planning offset `+6`, confirming that
the previous-Influence continuation retains the previous target's first byte,
which is the local site slot.

**Recreation status:** the complete family-5 handler and mode-7 selector are
live and replay-recorded, including duplicate previous-Influence destination
exclusion.

**Confidence:** High static evidence for the listed branches, selector fields,
Support scan/sum, target-pool asymmetry, terminal order, and RNG call order in
the fingerprinted version-1.1 executable; runtime corroboration remains
pending.

Family 10 at `0x0042a6e0` is now completely bounded at the action level. It
first calls selector `0x72`, which scans researched type-3 armor within the
gang's raw Tech and retains the first strict maximum Defense improvement. An
unequipped gang uses item 1 as its zero-Defense sentinel. The handler accepts
the returned armor only when its offset-`+14` cooldown is at most zero and raw
item cost is at most cash, then writes Equip and the literal cooldown 2. This
differs from the cost-times-three cooldown used by families 1, 3, 5, and 11.

If that opportunity fails, an empty miscellaneous slot plus a clear research
flag for item 44 writes Equip for **Smoke Bombs**. This special branch performs
no separate raw-Tech or cash comparison in the handler. Otherwise it writes
Heal only below Force 10, with effective Heal at least `-3`, and with cached
visible-opponent weight exactly zero in the current sector.

The remaining path calls sector mode 9 and compares selector 8 for the returned
sector against selector 8 for the current sector. Mode 9 scores only owned
sectors and sums each strictly positive Stealth value whose site is already
finished. If the returned sector's sum is strictly larger, the handler writes
Move and calls mode 9 a second time for the actual destination. It does not
reuse the probed destination, so a maximum tie can consume two bounded draws
and choose a different tied sector on the second call. If the probe is not
strictly better, selector `0x5b` counts same-sector records with previous
Chaos; zero writes Chaos and any positive count writes Hide.

**Recreation status:** the complete family-10 decision sequence, selector
`0x72`, fixed Smoke Bombs branch, literal armor cooldown, mode-9 score, double
selection, and Chaos/Hide fallback are live and replay-recorded. As with other
recovered handlers, behavior after a prepared command is unavailable under the
recreation's validator remains provisional.

**Confidence:** High static evidence for comparisons, scan/tie order, action
writes, and RNG order in the fingerprinted version-1.1 executable; runtime
corroboration remains pending.

Families 13 and 14 both use the fixed objective sets as Move destinations:
scenario value 8 selects modes 12/14 and is Big Man, while scenario value 6
selects modes 13/15 and is Eliminate. Family 13 uses the variants that exclude
already-owned objectives; family 14 uses the variants that retain them. This
establishes an original Eliminate movement bias toward every possible
headquarters location, not merely an attack-score bonus against a currently
visible Right Hands gang.

Instruction-level inspection of the terminal blocks at
`0x0040b87d..0x0040b9a6` and `0x004675c8..0x004676f1` establishes their exact
outer guard. Selector `0x1f` returns one only when a Big Man gang is in sector
27, 28, 35, or 36, or an Eliminate gang is in headquarters candidate 9, 12,
30, 33, 51, or 54. When that selector is not one and the handler has not already
written Equip, family 13 unconditionally replaces the planned command with Move
through mode 12/13; family 14 does the same through mode 14/15. Both handlers'
equipment blocks are themselves inside selector-`0x1f` branches, so an
off-objective gang cannot have written Equip before reaching this override.
The recreation therefore safely applies the complete off-objective terminal
path during replay-recorded planning and submits its exact one-step destination
through normal Move resolution.

Family 14 continues at `0x00467700..0x004677df` when selector `0x1f` is one
or the newly planned action is Equip. It changes the command to Heal and the
stored family to 13 exactly when the immediately previous action is Control,
Force is below 10, and effective Heal is at least `-3`. The repeated previous-
action query then rejects Attack, but that comparison is redundant after the
exact-Control guard. The recreation applies this terminal override during
replay-recorded preparation, including the family transition and normal Heal
resolution.

The shared owned-objective branch at family-13 lines `100..110` and family-14
lines `103..114` is also bounded. Strategic refresh `0x0040a1a7` caches
selector `0x90` per player and sector, and selector `0xaf` reads that cache.
Selector `0x90` scans other players and their 81 gang slots in ascending order;
for the first observer-visible gang in the requested sector it returns 10 for
a hostile human owner and 1 otherwise, or zero when no visible opponent is
present. When an objective sector belongs to the acting player and this cache
is zero, both handlers choose Heal before their equipment/site branches at
Force below 10 and effective Heal at least `-3`. This shared Heal path is now
live and replay-verified for both objective scenarios. Family 14 still applies
its later family-13 transition when the previous action was Control.

The preceding contested-objective branch is also bounded in both handlers.
Selector 2 returns scenario turns remaining. Only even absolute parity with a
nonzero cached selector-`0x90` weight enters target selection; the other path
writes Control immediately. Target selection makes as many as three inclusive
bounded draws. A hostile human-owned sector with cached weight 10 draws from
visible human gangs in the sector; otherwise it draws from visible gangs owned
by the sector owner. Selector `0x2b` evaluates each drawn ordinal against the
same ordinal in the full ascending visible-opponent list using
`(target Force + target Combat) / 4 - attacker Defense <= attacker Force +
attacker Combat - target Defense`. Its result controls only whether the retry
loop stops early: after three attempts the final selected gang is still used.
At Force 5 or higher that gang becomes the exact Attack target. With no selected
gang or lower Force, the handler chooses Heal when Force is below 10 and
effective Heal is at least `-3`, otherwise Control. Replay-recorded planning now
preserves the selected owner/roster-slot tuple, resolves it back to the stable
gang ID, and submits the normal validated Attack command. This branch may
attack a visible non-hostile sector owner, so recovered commands require
visibility but do not inherit the provisional fallback planner's hostility
filter.

When the acting player already owns the objective and visible opponents are
present, both handlers use the full ascending visible-opponent pool without the
remaining-turn parity gate. The shared loop starts from zero rather than two,
so this branch makes at most five bounded draws before applying the same Force,
Heal, and Control result selection. It is live for both families with exact
target replay.

When the owned objective has no visible opponent and the Heal gate fails, both
handlers try selector `0x61`'s weapon and selector 100 (`0x64`)'s armor in that
order. Each requires a nonpositive matching cooldown, a different item, enough
cash, and a previous action other than Attack; unlike family 1/11, a successful
objective Equip writes the literal cooldown 2. Selector `0x75` then chooses a
researched type-4 miscellaneous item within raw gang Tech whose Chaos bonus
strictly improves on the current item (item zero is the empty-slot baseline).
The caller applies the inclusive cash gate and no cooldown. If no affordable
miscellaneous upgrade exists, the handler scans the three local sites in slot
order, retaining the first strict maximum positive Support whose remaining
Resistance is positive, and writes Influence with that exact slot; otherwise
it writes None. These Equip/Influence targets now pass through normal validated
commands and authoritative replay. No Research action appears anywhere in
either complete handler.

`0x00408553` sorts the 64 sector scores descending while retaining their sector
indices. The caller chooses uniformly among every sector tied for the maximum;
a unique maximum consumes no RNG, while a tie consumes one bounded call (three
raw `rand()` calls). If record zero's strategic target is outside the immediate
3-by-3 neighborhood, the routine moves first along x and then independently
along y, retaining each component only when the resulting sector contains at
most five of the active player's gangs. It can therefore return a diagonal
neighbor without exceeding the six-friendly-gang capacity. If record zero is
adjacent and positive, the randomly selected maximum-tied sector is returned
directly. Candidate sectors are also removed late when marked unavailable or
when the active gang cannot strictly Control a non-owned destination under the
relevant gang-state branch. If those late filters leave a maximum below 1, the
routine still counts the maximum-tied entries, which are then all 64 zero-score
sectors, consumes one bounded draw, and runs the same x-then-y capacity routing
toward the drawn sector. It does not resume the radius search or return a
special sentinel.

`0x0040a1a7` proves the capacity field's identity: it clears all 64 integers at
`0x00489950 + player*0x100`, scans the player's 81 gang records, and increments
the integer indexed by each active gang's sector. The selector's four routing
comparisons against 5 therefore test destination occupancy, not terrain or a
pathfinding cost.

**Interpretation:** mode 5 is the general movement fallback recovered in the
family-1 continuity paths. Its exact neutral/owned/enemy ratio is 5:2:1, and
the routine separates strategic target scoring from the capacity-checked
single-tile Move ultimately queued. Replay-recorded AI preparation now uses
this kernel for all three live family-1 continuations. Randomness is used only
for mode-0 neighbor selection and equal-best final scores in the bounded paths
inspected here.

**Confidence:** High for the address, ring expansion, score-map sorting,
mode-0 directions, modes 1 through 5 weights, site-field offsets, human-player
count, per-player sector gang counts, unique-leader selector, mode-6 weights and owner
branches, fixed sector sets, direct call inventory, maximum-score tie
randomization, and x-then-y step return. Medium for the player-order
predicates, family-11 anchor, dynamic call arguments, and late candidate
filtering due to decompiler control-flow folding. Mode 10 and mode 16's
remaining family-11 guards are not yet fully labeled.

**Next validation:** reproduce the now-live objective routes and attacks plus
the mode-16 follower route as fixed original decisions, then continue bounding
the remaining family handlers before claiming runtime parity.

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

**Recreation status:** the equipment priority/cooldowns, Heal gate, and exact
first-visible local Attack target are live and replay-wired. Modes 10 and 16 are
also live; the separate six-by-81 formation-sector shorts preserve inactive
family records and are included in authoritative hashes, saves, and replays.

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
index 9 it rejects IDs 4 and 8 before returning the proposal. Slot zero accepts
its first proposal without balance validation. Slot one rejects a duplicate of
slot zero before validating the partial combination; slot two compares slot
zero and then slot one before validation. Every rejected proposal therefore
consumes its bounded draw before retrying. `0x00476516` rejects a partial
combination when the sum of any of the 14 site statistic modifiers is outside
-6 through +6.

**Interpretation:** Proposals are uniform and this generation path has no
frequency or class selector; duplicate and statistic rejection condition the
accepted second and third slots. Scenario 9 is Armageddon; its excluded Research
Lab and Science Center agree with the scenario's initially completed research.

**Confidence:** High static evidence; runtime reference fixture pending.

### BIN-CITY-003 - headquarters and Right Hands

**Observation:** `0x00439563` writes sector IDs 9, 12, 30, 33, 51 and 54.
`0x00476726` creates a six-value permutation by repeated bounded `1..6` draws
with duplicate rejection, maps players through that table, assigns ownership,
and replaces site slot zero with definition 21. `0x0046dc10` then initializes
gang definition zero in each player's assigned sector at Force 10.

**Interpretation:** Those six fixed sectors are the only new-game HQ candidates;
Right Hands is definition zero and always starts at maximum Force. In a local
new game all six slots are participants by the time this routine runs; setup
slots omitted by the local players have already become computer players.

**Confidence:** High static evidence; a runtime reference fixture remains pending.

### BIN-SETUP-001 - `SMGFUNDAGE` starting cash override

**Observation:** Fresh-game initialization assigns each player $500 in scenario
9 and $20 otherwise at `0x0046e766`. The adjacent name scan sets a transient
per-player byte only for the exact uppercase name `SMGFUNDAGE`. After the city
and player setup calls return, `0x0046ec72` reads that byte and overwrites the
player's cash with 0x5dc ($1,500). The transient byte has no save/load references;
the resulting cash value is what persists.

**Interpretation:** `SMGFUNDAGE` overrides both ordinary and Armageddon starting
cash. Case variants do not match, and no authoritative cheat flag is needed
after fresh-game bootstrap.

**Confidence:** High static evidence for the exact trigger, value, ordering, and
transient lifetime; runtime corroboration remains pending.

### BIN-SETUP-002 - local missing slots become computer players

**Observation:** The local setup handler `0x0040e0a0` keeps six player-type
dwords at `0x004ab638`. Type -1 is an empty setup slot, type 0 is a local human,
and type 1 is a computer. When Begin is accepted it scans slots 0 through 5;
every -1 slot is changed to type 1, receives a portrait from `0x00468c8e`, and
receives the default name for that portrait through `0x0046d1f7`. The portrait
helper repeatedly draws bounded `1..15`, subtracts one, and rejects any portrait
already present in any of the six slots. Thus all six players are active before
`0x0046dc10` generates the city, HQ permutation, and six Right Hands gangs.

The Win32 string-table names at resource IDs 62 through 76, indexed by portrait
0 through 14, are `ROCK`, `GECKO`, `RAZOR`, `SOUL TEAR`, `HEDIN VISE`, `REDD`,
`VECTOR`, `ICE`, `TORQ`, `KANSER`, `ECLYPSE`, `CRETIN`, `SCREAMER`, `PSYCHO`,
and `BLAKHART`. Portrait 15 is the empty-slot presentation image and is not a
candidate returned by the helper.

**Interpretation:** The original local setup count is the number of explicitly
configured local players, not the final match participant count. The recreation's
`OriginalMatchFactory` now completes omitted slots in ascending order and consumes
their portrait-selection RNG before AI state and city generation.

**Confidence:** High static evidence for types, ascending fill order, portrait
range, duplicate rejection, resource-name mapping, and placement before city
generation. Initial RNG seeding and a runtime setup fixture remain pending.

### BIN-SETUP-003 - `SMGISLANDS` neutral-sector Chaos override

**Observation:** The same exact, case-sensitive fresh-name scan sets transient
byte `0x004abc10` for `SMGISLANDS`. After city generation, assignment of all six
HQ owners, and creation of all six Right Hands gangs, `0x0046dc10` scans sectors
0 through 63 once for each flagged player. Every sector whose owner byte is -1
receives Chaos byte 100; owned HQ sectors are unchanged. The flag is cleared at
fresh setup/teardown and has no save/load references, while sector Chaos is
ordinary persisted state.

**Interpretation:** `SMGISLANDS` starts every neutral non-HQ sector at Chaos 100.
The recreation applies it after its six-participant local setup lifecycle, so all
six HQ candidates are already owned and remain at their generated Chaos value.

**Confidence:** High static evidence for the exact trigger, ordering, owner
predicate, value, and transient lifetime; runtime corroboration remains pending.

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

The six-byte array at `0x004a5ef0` is a persistent per-player modifier. Fresh
local-game setup clears each byte, compares that player's Pascal name with the
exact uppercase string `SMGMILK` at `0x0046e23f`-`0x0046e272`, and sets the
matching byte. The save reader/writer transfers all six bytes at `0x00463b6c`
and `0x00464071`; no per-turn or post-hire reset exists. At `0x00475aaa` the
resolver gives a flagged player's recruit Force 10 and bypasses the normal
bounded Force draw. The same name scan identifies adjacent original cheat
flags, corroborating that this is a name-triggered modifier rather than a
scenario or controller rule.

**Confidence:** High static evidence for arrays, sentinels, selected-slot writes,
mutual exclusion, resolver/refill order, call sites, RNG bounds, and the exact
`SMGMILK` trigger/effect; a controlled runtime sequence remains useful
corroboration.

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

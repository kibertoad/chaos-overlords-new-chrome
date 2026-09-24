# Randomness and turn structure

Status: active clean-room research log

The two things every other finding is timed against: where the original's
randomness comes from, and the order a turn runs in. The process seed, the
runtime random step and its bounded wrapper, every classified caller of that
wrapper, the fixed planning-slot order, and the boundaries that decide when a
recurring command is re-issued, replaced, or cleaned up.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Randomness and seeding](#randomness-and-seeding)
  - [BIN-RNG-001 - original process seed](#bin-rng-001---original-process-seed)
  - [BIN-RNG-002 - runtime random step](#bin-rng-002---runtime-random-step)
  - [BIN-RNG-003 - bounded random wrapper](#bin-rng-003---bounded-random-wrapper)
  - [BIN-RNG-004 - AI planning callers](#bin-rng-004---ai-planning-callers)
  - [BIN-RNG-005 - accepted local setup through initial city](#bin-rng-005---accepted-local-setup-through-initial-city)
- [Turn structure and phase order](#turn-structure-and-phase-order)
  - [BIN-COMMAND-ASSIGN-001 - recurring menus and replacement writes](#bin-command-assign-001---recurring-menus-and-replacement-writes)
  - [BIN-ENDTURN-001 - elimination cleanup, reports, and objective order](#bin-endturn-001---elimination-cleanup-reports-and-objective-order)
  - [BIN-HIDE-LIFECYCLE-001 - active and recurring action boundary](#bin-hide-lifecycle-001---active-and-recurring-action-boundary)
  - [BIN-REPEAT-001 - turn-start terminal recurring-command cleanup](#bin-repeat-001---turn-start-terminal-recurring-command-cleanup)
  - [BIN-TURN-PLAYER-ORDER-001 - fixed ascending planning slots](#bin-turn-player-order-001---fixed-ascending-planning-slots)
<!-- doc-index:end -->

## Randomness and seeding

### BIN-RNG-001 - original process seed

**Observation:** `GetTickCount`, `timeGetTime`, periodic multimedia timer APIs,
and asynchronous key state are imported. No external C runtime DLL appears in
the import table, so the C runtime RNG is statically linked. Process initializer
`0x00465620` calls `timeGetTime` at `0x004658fb`, zero-extends only the returned
AX/low 16 bits, and calls `0x00478cc0` at `0x00465905`. That adjacent runtime
function obtains the current thread data through `__getptd` and writes its sole
argument directly to `_holdrand`; it has no other incoming reference.

**Interpretation:** The original initializes its gameplay RNG once per process
from `timeGetTime() & 0xffff`, producing an unsigned seed from 0 through 65,535.
The other clock consumers remain presentation/network/timing candidates until
their individual data flow is classified.

The common startup caller `0x00460ccf` invokes this initializer at
`0x00460cf7`, then preference loader `0x0046439a` at `0x00460d14`. The loader
queries registry value `serialNum`; only when the resulting shared DWORD buffer
is zero, calls at `0x00464726` and `0x00464739` draw two inclusive `1..16384`
values and combine their zero-based forms into a legacy serial number. Each
bounded draw consumes three raw RNG values. The attempted registry write cannot
succeed because the key was opened with `KEY_READ`, and its result is ignored.
Moreover, failed queries do not have an isolated default buffer: `serialNum`
follows `prefsFullScreen` and can inherit stale query data when absent. Startup
therefore consumes either zero or six raw RNG values according to the effective
buffer value, not a reliable first-run/later-run distinction.

**Confidence:** High static evidence for the seed source, truncation, zero
extension, single writer, and process-initialization placement.

**Implementation:** Local-game startup captures the low 16 bits of the analogous
process-uptime millisecond clock when `ChaosGame` is constructed. Explicit
replay/test and multiplayer seeds remain full-width deterministic inputs.

**Intentional exception:** The recreation does not let an obsolete
installation-level network serial number perturb authoritative simulation RNG.
Its explicit seed always denotes the initial simulation stream. Native launch
fixtures must record the effective registry-query state, especially
`prefsFullScreen` and `serialNum`, and advance the predicted native stream by
two bounded calls only when the loader's final shared DWORD is zero.

**Next validation:** Correlate the first generated city against a native launch
whose low-16-bit seed and prior `serialNum` state are both captured.

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

**Confidence:** High for control flow, constants, range, consumption count, and
complete direct call-site ownership. Runtime output correlation remains open.

**Implementation:** `DeterministicRandom.NextRaw` and `NextInclusive` reproduce
these address-level facts. `DeterministicRandom.SeedFromTimerMilliseconds`
reproduces the original seed narrowing for local games; `MatchSetup.InitialSeed`
also remains an explicit modern deterministic input for tests, replays, and
multiplayer. `NextInclusive` also preserves the native below-one clamp and its
three raw draws; the recreation-only zero-based `NextInt` API retains strict
positive-bound validation.

Ghidra reports exactly 61 direct calls to `0x0045d227` in 24 containing
functions. All are now classified: 46 calls in 15 recovered AI dispatcher,
family, sector-target, and placement functions; 13 calls in eight simulation
functions covering setup portraits/reactions, city/site/HQ generation, hire
refill/Force, shared dice, and the whole-turn resolver; and the two conditional
startup `serialNum` calls above. No unknown direct gameplay wrapper caller
remains.

**Next validation:** correlate a controlled dice sequence with predicted
outputs; static call ownership is complete.

### BIN-RNG-004 - AI planning callers

**Observation:** The 46 direct wrapper calls in the AI group are confined to
dispatcher `0x00432da0`, shared sector/placement helpers `0x00408642` and
`0x00408214`, and the twelve recovered family-handler containers documented in
[Computer players](computer-players.md). Their action-selection branches, target pools, retry
loops, and persistent state writes have now been bounded individually. None is
an unclassified Combat or board-resolution caller.

**Interpretation:** Original-AI random consumption remains part of the same
global deterministic stream, but it is distinct from the whole-turn resolver's
dice and tie-break calls. The recreation keeps that separation while sharing
one serialized RNG state.

**Confidence:** High static evidence from the exhaustive incoming-reference
inventory and completed family/helper analyses. Fixed original-runtime AI
traces remain the missing black-box corroboration.

### BIN-RNG-005 - accepted local setup through initial city

**Observation:** Local setup handler `0x0040e0a0` accepts Begin, scans player
slots 0 through 5, and calls `0x00468c8e` only for an empty slot. That helper
draws bounded `1..15`, subtracts one, and retries while any slot already has the
portrait. After the handler returns true, its sole caller `0x00460ccf` loads a
status string and toggles UI state before calling outer match function
`0x0046e766` with the fresh-game flag. The intervening helpers and the outer
function's pre-fresh calls have no direct-call path to bounded RNG wrapper
`0x0045d227` within the checked eight-level call graph.

Inside fresh initializer `0x0046dc10`, the first RNG site is `0x0046dc83`: one
bounded `1..4` reaction draw for each of the six players, unless Homicidal
Maniac skips all six. It then calls city generator `0x00475fe1`, whose RNG paths
are the 40 density-center X/Y pairs followed by sector/site proposal draws, and
finally calls HQ permutation routine `0x00476726`. The bounded call-path report
finds exactly those direct/transitive wrapper paths from `0x0046dc10` before
the function returns. The offer filler remains later in `0x0046e766`.

The portrait helper's other caller is network/setup function `0x004677f0`; the
ordinary local path above does not pass through it. Interactive local setup
starts player zero at portrait zero, while configured human portrait changes
are explicit UI state rather than hidden random selections.

**Interpretation:** Given the RNG state at accepted local Begin, the original
pre-city stream is fully ordered: omitted-player portrait attempts, six
reaction draws (or zero at Homicidal), density centers, site proposals, and HQ
permutation. Rejection attempts consume normally at each stage. This does not
make the once-per-process seed equivalent to the state at Begin: an earlier
match or another RNG-using workflow in the same process can already have
advanced the original stream.

**Confidence:** High static evidence for the accepted-local-setup call chain,
consumer order, bounds, skips, and absence of an intervening bounded-RNG path.
The exact original state at Begin and city output still require a runtime
fixture because the seed is clock-derived and process-global.

**Implementation:** `OriginalMatchFactory.Create` treats `InitialSeed` as its
entry state, completes omitted slots in ascending order, then initializes AI,
generates the city, and assigns HQs in this recovered order. Fixed seed vectors
lock the recreation's complete setup result and final RNG state. Configured
human portraits are inputs and therefore do not replay prior UI interactions.

**Next validation:** capture the original RNG state context or an initial-city
fixture at accepted Begin; static analysis cannot recover a particular
clock-derived seed or prior process history.

## Turn structure and phase order

### BIN-COMMAND-ASSIGN-001 - recurring menus and replacement writes

**Observation:** The complete reference inventory for recurring action byte
`0x00498db2` finds human assignment writes only in individual-gang handler
`0x00414d8c` and sector-wide handler `0x0041462f`. The individual recurring
submenu maps exactly to Chaos (3), Control (4), Heal (7), Hide (8), Influence
(9), Research (11), and None (0). The sector-wide recurring submenu maps to the
same set except Research is absent. Neither recurring menu offers Bribe (2) or
Snitch (13).

The individual handler writes a selected recurring action and target to `+10`
and `+11`, then writes the same action to active field `+7`. Selecting None
zeros `+10`. Its direct one-off path zeros recurring action and target except
for its explicit recurring Influence shortcut. The sector-wide handler starts
its recurring value at zero; ordinary selections therefore overwrite active
action/target while clearing recurrence, whereas recurring selections write the
selected action to both active and recurring fields for each eligible gang.

**Interpretation:** A new assignment replaces the complete prior assignment;
it does not inherit the old repeat flag or target. Bribe and Snitch are ordinary
one-off actions even though raw recurring values 2 and 13 would survive the
turn-start terminal switch if introduced outside these human assignment paths.
The recreation rejects those UI-unreachable recurring forms rather than
promoting stale/raw-state behavior into a supported command.

**Confidence:** High from the exhaustive recurring-byte reference inventory,
both bounded assignment handlers, and their active/recurring target writes.

**Recreation status:** authoritative validation and the individual recurring
picker expose the recovered six actions. Queue replacement and cancellation
overwrite or clear the prior action, target, and repeat state atomically.

### BIN-ENDTURN-001 - elimination cleanup, reports, and objective order

**Observation:** The sole writer that clears player-active bytes is
`0x00476f3b`, called exactly once at line 936 near the end of whole-turn resolver
`0x00472775`. In scenario 7 (**Eliminate**), it scans players in slot order. If
roster slot zero no longer contains the active Right Hands record, it scans all
64 sectors, writing owner `-1` and zeroing all three site-progress bytes wherever
that player owned the sector, then scans all 81 gang records and writes only
inactive sector sentinel 100. It does not erase Force, definition, equipment,
or the remaining raw record fields.

The helper then applies the ordinary elimination predicate to every scenario:
a player stays active if it owns any sector or has any gang whose sector is not
100. Back in the resolver, lines 933-944 compare pre/post active bytes and append
type-9 elimination reports for all six recipients, player order first and
recipient order second. Only then does line 945 call end evaluator `0x00476857`.
That evaluator rebuilds scenario standings, tests the one-survivor rule, and
then tests the scenario-specific end condition. In Big Man, scorer `0x0047712a`
retains the persistent score instead of clearing it, adds current ownership of
the four center sectors, and the evaluator ends the match at score 40 or above.

**Interpretation:** Eliminate bulk retirement differs from combat death in its
trigger, reporting, and casualty accounting, but both leave stale equipment in
inactive records. That payload is neither recoverable nor credited back to
inventory, and normal roster-slot reuse resets it. The recreation has no
sector-100 gang state, so it represents the same terminal state with Force zero
and clears live queue/Hidden state, but preserves the three equipment fields for
native parity and final-state inspection. The Force/queue representation
difference has no playable effect; accumulated Big Man score, by contrast, is
visible in AI, ranking, and victory decisions and is preserved exactly.

**Confidence:** High static evidence from the unique active-byte writer, sole
call site, complete compact helper, end-of-resolver window, report loops, and
complete compact objective evaluator.

### BIN-HIDE-LIFECYCLE-001 - active and recurring action boundary

**Observation:** Each 32-byte public gang record stores its active action at
offset `+7` (`0x00498daf` base) and its recurring action at offset `+10`
(`0x00498db2` base). In outer turn function `0x0046e766`, decompiler lines
154-192 validate recurring actions and copy `+10` to `+7` for all six players
and all 81 roster slots before planning. Hide is action 8. Human recurring-menu
handler `0x0041462f` writes both fields immediately; its None branch sets both
to zero. One-off handler `0x00414d8c` clears `+10` for non-recurring choices
and writes the selected action to `+7`. Combat and police subsequently test the
same `+7` byte to decide whether the gang is hiding.

**Interpretation:** There is no independent delayed Hide flag in the native
record. Assigning Hide makes the gang hidden immediately. At the next turn
boundary, one-off Hide is replaced by recurring None, while recurring Hide is
copied back as the active action and remains hidden. Replacing or cancelling
Hide during planning changes the active action immediately and reveals the
gang. Resolution still increments the Hide statistic every turn that recurring
Hide executes.

**Confidence:** High from the bounded field writes, whole-roster turn-start
copy, action-8 dispatcher, and combat/police consumers in executable SHA-256
`A1430159BBE20869E277A5000311344F4EC141AB77C96B385336617149E97D89`
under Ghidra 12.1.3.

**Recreation status:** `MatchGangState.Hidden` now mirrors the active-action
lifecycle at assignment, cancellation, and Upkeep. Tests distinguish one-off
expiry from recurring retention and verify immediate replacement/cancellation.

**Next validation:** capture the targetability transition in a fixed native
hot-seat turn before and after replacing recurring Hide.

### BIN-REPEAT-001 - turn-start terminal recurring-command cleanup

**Observation:** Before copying recurring action `+10` to active action `+7`,
outer turn function `0x0046e766` scans all six players and all 81 roster slots
in fixed order. Its switch contains terminal checks for exactly four retained
actions:

- Control (4) clears when the acting player already owns the gang's sector or
  the sector's Crackdown byte is positive;
- Heal (7) clears when Force equals 10;
- Influence (9) clears when site progress has reached base Resistance or the
  sector owner is no longer the acting player; and
- Research (11) clears when the selected item's remaining-progress byte is zero.

After that switch, every recurring action clears when the gang's sector byte is
the inactive sentinel 100. The surviving recurring action and target are then
copied into the active fields. Chaos and Hide have no terminal case and continue
until explicitly replaced/cancelled or the gang becomes inactive. Bribe and
Snitch also lack terminal cases, but the native human assignment menus cannot
place either value in the recurring field. The cleanup runs before the same
outer function's Crackdown-duration update and pre-planning site-benefit rebuild.

**Interpretation:** A Control order that becomes illegal because police appeared
after submission fails during resolution but does not wait out the police; it is
discarded at the following turn start even if that update would expire the
Crackdown. Likewise, unfinished recurring Influence is discarded after an
overthrow rather than resuming automatically if ownership is later regained.

**Confidence:** High static evidence for action IDs, predicates, player/roster
order, inactive sentinel, copy order, and placement before Crackdown/site updates.

**Recreation status:** turn-start normalization now implements all four native
terminal checks. Successful Control, Heal, Influence, and Research may still be
released immediately after resolution as an unobservable internal optimization;
the next planning state is identical.

### BIN-TURN-PLAYER-ORDER-001 - fixed ascending planning slots

**Observation:** After Upkeep preparation, outer turn function `0x0046e766`
clears six per-player presentation bytes at lines 309-311, then loops
`local_c` from 0 through 5 at lines 312-348. Eligible controller value 1 calls
the computer planning path `0x00458fa0`; value 0 calls human handler
`0x0046fd80`; inactive/elimination states do not enter either ordinary planning
handler. The alternate result/handoff loop at lines 433-448 likewise scans
slots 0 through 5. Only after the ordinary planning loop does the function call
the monolithic whole-turn resolver, whose action and hire passes independently
scan player slots in the same ascending order.

**Interpretation:** Human/computer mixtures do not reorder a turn: participating
players plan in fixed player-slot order, eliminated slots are skipped, and all
deferred resolution follows after the planning scan. The recreation exposes
Command, Execution, and Hire as explicit deterministic boundaries, but advances
the same ascending slot sequence and automatically records transitions across
eliminated slots. Command replacement sequence records only a player's
submission chronology for replay/persistence; it does not choose resolution
order. Each mutating phase restores the recovered action-specific player/roster
scan (with its independently documented Control sector and Movement Terminate
passes) before consuming RNG or changing state.

**Confidence:** High static evidence for both outer-loop bounds, controller
dispatch branches, inactive-state exclusion, and the resolver call boundary;
recreation turn-flow fixtures cover automatic skips.

**Hire-boundary corroboration:** The outer turn function performs this cash/
Upkeep scan at the top of each non-initial loop iteration, before entering the
six player planning handlers. After all planning handlers return, its direct
call at `0x0046f706` enters whole-turn resolver `0x004726c0`; that wrapper's
call at `0x00472750` enters hire resolver `0x00472775`. A successful hire copies
a complete 32-byte gang record into the first free roster entry and writes its
sector byte at record offset `+2` to the selected destination rather than the
inactive sentinel 100. On the next outer-loop iteration, the Upkeep scan's
active predicate sees that new record and subtracts its definition's signed
Upkeep field. Therefore the original charges no Upkeep before or inside the
hire resolver, but it does charge the recruit at the immediately following
turn-start Upkeep. The original UI returns control only after that scan, which
can make the contract price and first Upkeep deduction appear simultaneous.

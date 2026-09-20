# New-game setup and city generation

Status: active clean-room research log
Last updated: 2026-09-20

Everything between a fresh setup screen and the first playable turn: city
generation from the density field, site rejection sampling, headquarters and
Right Hands placement, the shipped setup defaults, the exact-name modifiers,
the local player-card interaction geometry, the legacy session-lobby and
transport flows, and hot-seat handoff ordering.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [City generation](#city-generation)
  - [BIN-CITY-001 - density-derived sector income and tolerance](#bin-city-001---density-derived-sector-income-and-tolerance)
  - [BIN-CITY-002 - three-site rejection sampling](#bin-city-002---three-site-rejection-sampling)
  - [BIN-CITY-003 - headquarters and Right Hands](#bin-city-003---headquarters-and-right-hands)
- [Setup defaults and exact-name modifiers](#setup-defaults-and-exact-name-modifiers)
  - [BIN-SETUP-000 - fresh setup defaults to Kill 'Em All](#bin-setup-000---fresh-setup-defaults-to-kill-em-all)
  - [BIN-SETUP-001 - SMGFUNDAGE starting cash override](#bin-setup-001---smgfundage-starting-cash-override)
  - [BIN-SETUP-002 - local missing slots become computer players](#bin-setup-002---local-missing-slots-become-computer-players)
  - [BIN-SETUP-003 - SMGISLANDS permanent neutral-sector Crackdown](#bin-setup-003---smgislands-permanent-neutral-sector-crackdown)
  - [BIN-SETUP-004 - extra-gang and global-visibility name modifiers](#bin-setup-004---extra-gang-and-global-visibility-name-modifiers)
- [Setup screens and legacy session flows](#setup-screens-and-legacy-session-flows)
  - [BIN-SETUP-005 - exact local player-card interaction geometry](#bin-setup-005---exact-local-player-card-interaction-geometry)
  - [BIN-SETUP-006 - legacy session lobby resources are distinct flows](#bin-setup-006---legacy-session-lobby-resources-are-distinct-flows)
  - [BIN-SETUP-007 - legacy transport progress sheets](#bin-setup-007---legacy-transport-progress-sheets)
  - [BIN-SETUP-008 - legacy transfer spinner animation](#bin-setup-008---legacy-transfer-spinner-animation)
- [Hot-seat handoff](#hot-seat-handoff)
  - [BIN-HOTSEAT-002 - private handoff ordering and terminal-player path](#bin-hotseat-002---private-handoff-ordering-and-terminal-player-path)
<!-- doc-index:end -->

## City generation

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

## Setup defaults and exact-name modifiers

### BIN-SETUP-000 - fresh setup defaults to Kill 'Em All

**Observation:** In EXE-GOG-1.1, the initialized preference byte at
`0x00487858` is `0`. Preference loader `0x0046439a` replaces it with the
registry value `prefsObjective` when that value exists. Setup initializer
`0x004384c0` copies the signed byte into the active scenario dword at
`0x004abbe8` before rendering the full local setup screen. The earlier analysis
incorrectly mapped that zero through the recreation's gameplay enum rather
than the original setup's visual-button order.

**Runtime observation:** On 2026-09-13 the fingerprinted original opened its
full local setup with Kill 'Em All selected. Its first visual-button light was
the only bright scenario light, and the description panel named the same
objective. Five desktop-copy frames taken 120 ms apart were byte-identical
(PNG SHA-256
`ab81528fa770e991140d5133c5dbb092751c7c3f723195cb33dcb41740410275`).
Targeted searches found no `prefsObjective` value in that user's HKCU or the
machine hive, excluding a stored objective override for this observation.

**Interpretation:** A fresh original setup selects the first visual scenario,
Kill 'Em All. The initialized zero is a setup/original ordering value and must
not be interpreted through the recreation's enum, where Greed happens to be
zero. A stored `prefsObjective` may still restore another selection when it
exists.

**Confidence:** High from the verified GOG 1.1 initialization path plus a
stable original-runtime observation with the override value absent. This
corrects the earlier static interpretation without changing the recovered
initialized byte or preference-loader facts.

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
generation. A runtime setup fixture with captured seed and prior `serialNum`
state remains pending; `BIN-RNG-001` and `BIN-RNG-005` now recover the seed and
accepted-Begin call order statically.

The original Help further specifies the interactive local-player side of this
state machine: setup begins with one local human; Add introduces another local
human and Remove reverts the last one; clicking the name under a face edits at
most 10 characters. The resource-name helper at `0x0046d1f7` also passes an
exact length of 10 to `0x00466673` when filling an omitted computer's 12-byte
name record. The client now starts with one configured human, adds/removes
humans rather than synthetic CPU toggles, restricts selectable portraits to 0
through 14, and exposes the bounded name field. Face dragging now moves a human
identity into an empty color or exchanges two human colors; the transient sparse
setup is normalized into ascending slots before recovered empty-slot completion.
Exact name/drop-field coordinates still require native capture.

### BIN-SETUP-003 - `SMGISLANDS` permanent neutral-sector Crackdown

**Observation:** The same exact, case-sensitive fresh-name scan sets transient
byte `0x004abc10` for `SMGISLANDS`. After city generation, assignment of all six
HQ owners, and creation of all six Right Hands gangs, `0x0046dc10` scans sectors
0 through 63 once for each flagged player. Every sector whose owner byte is -1
receives byte 100 at sector offset `+0xf` (`0x004a08f7`); owned HQ sectors are
unchanged. The whole-turn resolver reads this same byte as police duration and
decrements only positive values below the permanent sentinel 100. The flag is
cleared at fresh setup/teardown and has no save/load references.

**Interpretation:** `SMGISLANDS` starts every neutral non-HQ sector under a
permanent Crackdown. It does not initialize a sector-Chaos value. The override
runs after the six HQ candidates become owned, so those starting sectors remain
unaffected.

**Confidence:** High static evidence for the exact trigger, ordering, owner
predicate, value, and transient lifetime; runtime corroboration remains pending.

### BIN-SETUP-004 - extra-gang and global-visibility name modifiers

**Observation:** Fresh-game initializer `0x0046dc10` compares every Pascal player
name case-sensitively with three additional uppercase strings. `SMGSPANK` sets
byte `0x004abbd8`; after the normal slot-zero Right Hands is created, slots 1
through 5 receive identical definition-zero, Force-10, unequipped Right Hands in
that player's HQ sector. `SMGKICKASS` sets byte `0x004a2788`; its slots 1 through
5 instead receive gang definition 59 (GROUND ZERO), Force 10, weapon item 23
(PLASMA GENERATOR), armor item 37 (BATTLE SUIT), and miscellaneous item 52
(EMPATHIC ENHANCER), again in the HQ. The flags are transient and the resulting
gang records are ordinary persisted state.

`SMGHUBBLE` sets per-player byte `0x004ab588`. Visibility rebuild routine
`0x0046fa11` normally initializes each of that viewer's 64 sector-detection
entries to -32000, but initializes all of them to 1000 when the byte is set. It
then folds in the viewer's active-gang Detect values using the exact algorithm
documented in `BIN-DETECT-001` and marks every opposing active gang visible when
its Stealth is no greater than the sector value. The
six-byte modifier array is transferred by save writer/reader references
`0x00463b5b` and `0x00464060`.

**Interpretation:** Exact `SMGSPANK` and `SMGKICKASS` give mutually exclusive
six-gang openings without RNG draws. Exact `SMGHUBBLE` grants its player global
opponent visibility; the recreation derives this persistent behavior from the
persisted exact player name rather than adding a redundant flag.

**Confidence:** High static evidence for exact triggers, gang slots, definitions,
Force, loadouts, sector placement, visibility baseline, and persistence; runtime
corroboration remains pending.

## Setup screens and legacy session flows

### BIN-SETUP-005 - exact local player-card interaction geometry

**Observation:** Full local-setup handler `0x0040e0a0` constructs six card
origins at `(397,94)`, `(480,94)`, `(397,168)`, `(480,168)`, `(397,242)`, and
`(480,242)`, then tests a 64-by-68 half-open rectangle at each origin. Within
an occupied human card, vertical offset below 58 enables the portrait controls:
horizontal offset below 16 calls decrement helper `0x00468d87`, while offset
strictly greater than 48 calls increment helper `0x00468cfc`. Vertical offset
58 through 67 calls the ten-character name editor at `0x0040f63d`. Therefore
the native hit regions are 16-by-58 on the left, 15-by-58 on the right, and
64-by-10 along the bottom; they intentionally exceed the visible 12-by-18
arrows and 64-by-8 text baseline.

The name editor clears its modal buffer before opening dialog resource `0x8b`.
On acceptance, `0x0040f63d` copies only a non-empty buffer; accepting an empty
editor therefore leaves the existing player-name record unchanged. The local
setup follows that behavior instead of substituting a generated `PLAYER#n`
default.

Before applying those click actions, sole-call drag helper `0x0040f72e` waits
while the pointer stays in the half-open four-pixel box from initial offset -2
through +1. Leaving that box while the button remains down begins a drag. The
helper draws a 40-by-40 portrait token centered at the pointer, clamps its
center to x 20..620 and y 20..440, and on release tests the same six 64-by-68
card rectangles. A valid destination swaps the complete type, portrait, and
12-byte Pascal-name record, including an empty slot; release outside every card
leaves the roster unchanged. A click action is selected from the original press
point only when the drag helper reports that no drag began.

Global selected-card index `0x004854c4` starts at zero. A click on another
occupied card only selects and redraws it; arrow/name actions occur only when
the pressed card was already selected. Add selects the newly occupied slot.
Remove deletes the selected slot and selects the highest remaining occupied
slot. A successful drag selects its destination after moving or exchanging the
complete identity record.

Renderer `0x0040ee8a` copies every 32-by-32 top-strip portrait opaquely. For
each occupied local card it takes source `(32 * portrait,480,32,30)` and scales
it opaquely to `(cardX,cardY + 3,64,60)`. Only the selected card then receives
the exact-white-keyed `PX00140` arrow overlay from `(220,138,64,62)` at that
same destination origin. The non-selected branch requests mode 0, but because
its source and destination sizes differ the wrapper's opaque scaling branch
precedes and bypasses mode selection.

**Interpretation:** Bitmap apertures describe drawing, not input. The former
recreation reused the smaller visible glyph rectangles for input, began setup
drags only after four pixels of absolute motion, used a 48-pixel token, and did
not permit a drag to begin over arrow/name bands. It also acted on every card's
subcontrols immediately, drew arrows on every card, stretched all 32 portrait
rows over the full face, and incorrectly applied the `PX00146` inactive-seat
stencil to empty local top slots. The input and compositor now follow the
recovered selected-card state, source/destination rectangles, and keyed arrow
overlay.

**Confidence:** High from the complete local handler, the drag helper's sole
caller and bounded loop, the two portrait helpers, and the name editor.

**Next validation:** Native cursor imagery and drag-target feedback remain
visual capture work; card selection, hit testing, composition, and drag/drop
mechanics are statically closed.

### BIN-SETUP-006 - legacy session lobby resources are distinct flows

**Observation:** Title loop `0x00460ccf` directly calls the three unrelated
handlers `0x004677f0`, `0x0040b9c0`, and `0x00456f80`; their only shared
property is that each owns a legacy network/session path. The first loads
`PX00144` and starts host-side channels before showing the `Hosting at IP
Address` message. It starts with one configured assignment, edits no more than
four, and keeps polling participant state before it allows launch. Its four
64-by-68 record cells are `(397,94)`, `(480,94)`, `(397,168)`, and `(480,168)`.

`0x0040b9c0` instead negotiates one of three connection modes before it loads
`PX00145`. It owns a compact four-record editor at `(251,124)`, `(334,124)`,
`(251,198)`, and `(334,198)`. Both editors keep the broad 16-pixel left,
15-pixel right, and 10-pixel bottom portrait/name bands recovered for local
setup, but write legacy assignment state, not local player cards. Their four
vertical action rectangles differ: `PX00144` uses `(254,371,24,92)`,
`(254,468,24,92)`, `(375,370,45,92)`, `(375,468,45,92)`; `PX00145` uses
`(284,225,24,92)`, `(284,322,24,92)`, `(345,224,45,92)`, `(345,322,45,92)`.
Both call their own four-case held-button helper, copy the pressed image only
while the pointer remains in that rectangle, and issue the normal slot-2 press
cue before a rejected seat-count operation emits slot 4.

`0x00456f80` loads `PX00146`, uses the separate unscaled six-seat renderer
`0x00457b7b`, and continuously processes connection records. Its two controls
are `(230,224,45,92)` and `(230,322,45,92)`: the upper path continues the
session, while the lower path closes all twelve tracked connections before
returning. Helper `0x00457eed` provides their held/release-inside behavior,
with the normal image coming from surface 1 and pressed image from surface 7.

**Interpretation:** `PX00144` is the legacy host lobby, `PX00145` is a compact
legacy connection/session editor, and `PX00146` is the legacy participant-ready
view. They are not player-count variants of one another or of `PX00143`, and their
layouts must not leak into local setup. Their distinct session setup and
connection lifecycle corroborate the existing scope decision that original
transports and wire formats are not recreation compatibility targets.

**Confidence:** High from the complete three handlers, their sole title-loop
calls, resource loads, bounded pointer helpers, host string, connection loops,
and renderer ownership.

**Recreation status:** The modern online flow is intentionally independent. No
legacy transport, screen record encoding, or compatibility claim is added.
Options may select a classic lobby presentation: it renders the original
`PX00144` host-lobby sheet and maps its choice/action faces to the modern
public/private listing, late-join, join-key copy, rules, start, and leave
operations. The six-seat roster and every operation still use the modern
session state; the original four-seat connection lifecycle remains unrouted.

### BIN-SETUP-007 - legacy transport progress sheets

**Observation:** `PX00137` is loaded only by `0x0040ced0` and
`0x0040d72f`, both reached through the original legacy connection/setup paths.
The 220-by-72 sheet is copied to the centered screen rectangle
`(210,60)-(430,132)`. Their shared helper `0x0040d3c0` copies a three-pixel
wide-as-progress segment from the `PX00129` green strip at `(354,0)` into the
frame at x=298/y=105 and selects one of the legacy status-text pairs for modes
0 through 4 or 10 through 13.

`PX00139` is likewise loaded only by the legacy connected-session branches
`0x0046a7cb` and `0x0046d22f`; `0x004726c0` selects those branches instead of
the normal local whole-turn resolver when its legacy-session state is set. Its
shared renderer `0x0046d77b` scans all six connection slots. Each connected
slot with a positive progress value receives its own three-pixel green segment
at x=298, on rows y=96 plus four times the slot; other slots are restored from
the panel's fixed empty-bar art. It also selects the same bounded legacy status
text family. No local-game or modern-client caller reaches either renderer.

**Interpretation:** `PX00137` is the single legacy transport transfer-progress
frame and `PX00139` is its six-seat synchronization counterpart. They are not
city/site meters or a presentation contract for the modern public-lobby or
join-key protocol.

**Confidence:** High from the complete resource-loader call census, caller
paths through legacy setup/session branches, exact copy rectangles, and both
bounded progress renderers. Native timing and text phrasing are irrelevant to
the explicitly unsupported transport.

**Recreation status:** Both sheets remain extracted as original artifacts but
are deliberately unrouted. The modern multiplayer flow supplies its own safe,
observable session feedback and never claims legacy wire compatibility.

### BIN-SETUP-008 - legacy transfer spinner animation

**Observation:** Every load of the 720-by-48 `PX00138` sheet occurs beside a
legacy progress frame: simple transfer paths `0x0040ced0` and `0x0040d72f`
also load `PX00137`, while connected-session paths `0x0046a7cb` and
`0x0046d22f` also load `PX00139`. The complete loader census has no other
caller.

Each path maintains a frame counter from zero through fourteen, wrapping to
zero. On its timed update it copies the corresponding 48-by-48 source cell
from `PX00138` into the fixed spinner aperture at `(224,72)-(272,120)`. The
sheet is therefore a fifteen-frame activity animation, rather than the
general command-icon artwork previously attributed to it. In the host/client
synchronization route it continues alongside the six-seat bar renderer;
neither the normal local whole-turn resolver nor any modern-client route loads
or advances it.

**Interpretation:** This spinner visualizes work in the original transfer
protocol. It is not a game-action affordance and is not evidence for a direct
connection UI in the recreation.

**Confidence:** High from the complete resource-load census, all four bounded
counter/copy loops, their paired frame resources, and the explicit exclusion
of the normal local resolver.

**Recreation status:** `PX00138` remains an extracted historical artifact and
is deliberately unrouted. Modern hosted sessions keep their separate,
observable startup feedback rather than imitating an unsupported transport.

## Hot-seat handoff

### BIN-HOTSEAT-002 - private handoff ordering and terminal-player path

**Observation:** The outer local-game loop at `0x0046e766` counts active local
players before it starts their planning visits and enables `PX00132` only when
more than one remains. For an ordinary active local player it calls the
`PX00132` presenter at `0x004396c0`, records that player as the current viewer,
and then enters planning at `0x0046fd80`. The presenter itself is a blocking
event loop: its sole Ready control uses the slot-2 pressed-control helper at
`0x00439f7a`, and neither its keyboard nor pointer paths continue until that
control completes or the outer menu/quit state interrupts it.

Planning entry makes the ordering unambiguous. Its one-time new-local-game
flag first opens Game Information `0x0045519d`; this occurs after Ready, not
before the privacy card. It then invokes either the simple Combat Results
presenter `0x00451f80` or Detailed Combat presenter `0x0042e040`, selected by
the Detailed Combat option. `0x00451f80` silently
returns for an empty eligible-sector set when called in this automatic mode;
when results exist, its panel event loop returns before planning continues.
Only after that call returns does `0x0046fd80` rebuild its complete 32-byte
Last Turn Events read-state table at `0x00494870` and open Last Turn Events at
`0x0044f2fc`. Its next branch tests the per-player unread flag and calls
general-sound slot 6 through `0x00464290`; it is therefore after both blocking
presenters, not merely after Ready. The same function resets the alert cadence
counter immediately after that call.

The outer loop's ordered slot walk makes terminal timing precise. It visits
slots 0 through 5, and when it reaches an unretired local elimination it shows
`PX00132` in a multi-local game, then calls `0x0042c3f5`, retires that slot,
and resumes the scan for later slots. Immediately afterwards it requests
music-selector mode 2 (`0x004642bd`), the established gameplay Track 3-8
program, rather than notifying a later player. It is not a popup at resolution
time and is not silently skipped. That presenter first preserves the existing
city screen, lays `PX00200` at `(x=106, y=25)`, then lays `PX00203` at
`(x=110, y=30)`, centers the eliminated player's name at `(158, 46)`, and
composites the 64-by-64 Overlord portrait at `(126, 54)`. It blocks until the
shared `DONE` input rectangle `(x=428..527, y=377..424)` completes; the card
itself is not a whole-surface continue target.
Single-local play bypasses the handoff gate but still reaches this elimination
presenter when it loses.

The final awards controller at `0x0042b9e0` independently counts human
participants. With exactly one it asks renderer `0x0042ce61` for the
single-player `PX00202` victory presentation; with multiple local humans the
same controller renders `PX00200`/`PX00201` shared standings directly. Thus
the private `PX00203` path is a mid-match/local-elimination path, while direct
shared awards are the correct final hot-seat behavior.

**Interpretation:** `PX00132` is a privacy boundary for a locally controlled
turn, not merely a decorative next-player card. For a normal hot-seat handoff,
the order is **Ready -> one-time Game Information (new local game only) ->
automatic combat (Simple or Detailed) -> Last Turn Events -> planning city**.
Combat and events remain separate blocking/private
presentations; events must not be shown first just because both queues are
nonempty. An eliminated local player also owns their terminal presentation
before their seat is retired. Final multi-local results do not show individual
victory splashes: they go directly to shared awards/statistics.

**Confidence:** High static evidence for the multi-local gate, Ready control,
normal and terminal call paths, and Combat-before-Events order. Exact native
animation cadence and the terminal-screen artwork timing still require runtime
capture.

**Recreation status:** Automatic handoff routing now follows the native
active-human gate as well as the native start order: computer opponents and
retired local seats do not cause a solo local player to see `PX00132`, and a
new local multiplayer game shows Game Information only after that card's
Ready control. Simple Combat opens `PX05012` and continues to Last Turn Events only
when its private panel closes. Detailed Combat starts its bounded recreation
presentation over the city and then follows the same event chain; its
non-blocking/skippable behavior remains an explicit modern safety correction
for the known native detailed-combat freeze. The hot-seat coordinator now
reports eliminated command slots in native order, and the UI presents each
eliminated human as `PX00132` handoff, `PX00203`, then the later slot, while
completed multi-local games continue to shared awards. The shared results
frame and both private cards now use the recovered native composition
coordinates and player-name placement. The existing handoff portrait, Ready
press cue, per-viewer combat state, and unread-Comlink delay remain in place.
The recreation holds the presentation-only unread-Comlink cadence dormant
through the automatic Game Information, Combat, and Events route, then starts
it only when that player reaches the planning city; ordinary manually opened
panels do not affect an already-active cadence.
The recovered `0x0041b8bc` timer arm sits after that same slot-6 alert and
writes `timeGetTime()` only once the automatic route has completed, so the
recreation arms its optional planning timer at the shared city-entry boundary.
The native pointer targets for Awards, Stats, and Done are now also recovered;
Enter/Space presentation navigation remains the separately documented
compatible-keyboard QoL layer.

The Stats tab copies the opaque `PX00201` `(x=96, y=112, 160x64)` strip to
`(x=262, y=30 + 66 * displayed-row)`. The five values occupy the original
fixed-width 8/8/7/6/6 digit fields, all ending at `x=419`, at vertical offsets
37, 46, 58, 67, and 79. The recreation uses that strip and preserves the
native field widths rather than redrawing an approximate label grid.

The shared results renderer prints the unmodified player name at `(197,
38 + 66 * displayed-row)`; it does not prefix a textual place. For ranked
players it separately copies the 16-by-32 `PX00201` marker from
`(16 * (place - 1), 48 + 32 * player-slot)` to `(113, 31 + 66 * displayed-row)`.
Inactive rows have no marker. The recreation keeps competition standings for
ordering and tie semantics while using this native visual representation.

**Next validation:** Capture a multi-local elimination followed by another
human turn to corroborate `PX00203` timing and later-slot timing, and capture
Simple and Detailed handoffs containing both a combat result and an event
report to corroborate modal completion and timing.

# Turn reports, Comlink, and Search

Status: active clean-room research log
Last updated: 2026-09-20

The per-player information channels: the Last Turn report table with its types
and lifetime, its pager and exit control, the Comlink message queue with its
capacity, overflow, navigation, composition, and projection rules, and the
Search panel's per-player site filters, controls, and row targets.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Last Turn Events](#last-turn-events)
  - [BIN-EVENT-001 - Last Turn report table, types, and lifetime](#bin-event-001---last-turn-report-table-types-and-lifetime)
  - [BIN-EVENTS-002 - Last Turn Events pager and exit control](#bin-events-002---last-turn-events-pager-and-exit-control)
  - [BIN-EVENT-003 - cash-failure report illustration](#bin-event-003---cash-failure-report-illustration)
- [Comlink](#comlink)
  - [BIN-COMLINK-001 - per-player message queue capacity and overflow](#bin-comlink-001---per-player-message-queue-capacity-and-overflow)
  - [BIN-COMLINK-002 - View navigation and hit geometry](#bin-comlink-002---view-navigation-and-hit-geometry)
  - [BIN-COMLINK-003 - Send eligibility, controls, and composition cursor](#bin-comlink-003---send-eligibility-controls-and-composition-cursor)
  - [BIN-COMLINK-004 - View record fields and projection](#bin-comlink-004---view-record-fields-and-projection)
- [Search](#search)
  - [BIN-SEARCH-001 - per-player site filters and city markers](#bin-search-001---per-player-site-filters-and-city-markers)
  - [BIN-SEARCH-002 - exact Search panel controls and row targets](#bin-search-002---exact-search-panel-controls-and-row-targets)
<!-- doc-index:end -->

## Last Turn Events

### BIN-EVENT-001 - Last Turn report table, types, and lifetime

**Observation:** The original Last Turn store is a six-player table rooted at
`0x004aae08`, with player stride `0x140` and exactly 32 ten-byte records per
player. Each record contains an occupied byte, a two-byte report type, and
three two-byte arguments. Recorder `0x00477748` appends at the per-player count
in `0x004abca8`; when the count is already 32 it returns without shifting or
overwriting anything. Thus the first 32 reports survive and later reports in
the same resolution are discarded.

Reset wrapper `0x004726c0` clears every occupied byte immediately before it
enters the whole-turn resolver. The resolver `0x00472775` then zeroes all six
counts before any report calls. All 12 direct recorder call sites are inside
that resolver. The UI handler `0x0044f2fc` later scans all 32 occupied bytes for
the requested player in ascending slot order; it does not merge reports from
an older resolution.

Compositor `0x0044fd6c` has cases 0 through 9. Its exact Win32 string-table
labels and statically traced meanings are:

| Type | Original status | Trigger |
|---:|---|---|
| 0 | `NO EVENTS.` | Empty/default compositor case; not emitted by the resolver |
| 1 | `POLICE CRACKDOWN.` | Crackdown created; players with a gang in the sector at resolution opening |
| 2 | `SECTOR CONTROL ATTAINED.` | Sector captured |
| 3 | `SECTOR CONTROL LOST.` | Previous owner displaced |
| 4 | `SITE COOPERATION ACHIEVED.` | Influence completed |
| 5 | `RESEARCH COMPLETED.` | Research completed |
| 6/1 | `INSUFFICIENT CASH TO BRIBE.` | Bribe cash failure |
| 6/2 | `INSUFFICIENT CASH TO EQUIP.` | Equip cash failure |
| 6/4 | `INSUFFICIENT CASH TO HIRE.` | Hire cash failure |
| 7 | `UNABLE TO HIRE, SECTOR AT CAPACITY.` | Hire sector full |
| 8 | `UNABLE TO HIRE, MAX GANGS REACHED.` | Player gang limit reached |
| 9 | `PLAYER HAS BEEN ELIMINATED.` | Player eliminated; recorded for all six slots |

There is no generic command-failure or objective-update report type. Target
evasion, unavailable items, movement failure, and other rejection reasons do
not enter Last Turn Events through this table.

**Interpretation:** Last Turn Events is a bounded projection produced anew by
the immediately completed whole-turn resolution, not the complete mechanical
notification history. Its overflow policy is keep-first, not a ring buffer.

**Confidence:** High static evidence from table strides, both reset loops, the
complete recorder and caller inventory, the compositor switch, and executable
string-table resources 33 through 44. Runtime corroboration remains useful for
presentation timing, not for type membership or capacity.

**Implementation:** `LastTurnEventProjection` filters only the recovered native
types from the immediately completed turn, preserves emission order, and keeps
the first 32. The broader authoritative `NotificationQueue` remains a
recreation mechanism and is deliberately not misrepresented as the native
table. Status text now matches the executable strings exactly.

### BIN-EVENTS-002 - Last Turn Events pager and exit control

**Observation:** The complete `PX05010` handler `0x0044f2fc` translates a
pointer inside the shared panel and tests only three controls: previous local
`(31,33)-(57,56)`, next `(59,33)-(85,56)`, and bottom exit
`(33,169)-(82,191)`. It applies the same bounded page behavior as Combat
Results and Comlink View: first/last boundary attempts play slot 4, while a
legal step uses helper `0x00451602` and slot 3. Enter and Execute activate the
bottom exit face. No Delete rectangle or report-clearing pointer branch occurs
in this handler.

**Interpretation:** Last Turn Events has one 49-by-22 bottom control, not a
generic Cancel/Delete pair. Reports remain available until the normal review
completion lifecycle processes them; the recreation does not add a destructive
per-panel delete path.

**Confidence:** High static evidence for all three rectangles, keyboard and
pointer branches, sound outcomes, and absence of a Delete branch from complete
handler `0x0044f2fc`; native capture remains useful for pressed-state
presentation.

### BIN-EVENT-003 - cash-failure report illustration

**Observation:** In the reference executable, the Last Turn compositor
`0x0044fd6c` keeps the report type in local `local_10c`. Apart from its special
type-4 and type-5 branches, its generic illustration branch calls the resource
loader with `local_10c + 6000`. The cash-failure entries recorded as type 6 in
BIN-EVENT-001 therefore load `PX06006`, a 242-by-158 PX16 presentation image.

**Interpretation:** Bribe, equip, and hire insufficient-cash reports all show
`PX06006` in the Last Turn Events artwork aperture. It is the empty-safe
illustration; leaving that aperture black is not native behavior.

**Confidence:** High static evidence from the complete compositor's type value,
special-case branches, and generic resource-load arithmetic. The descriptive
empty-safe name is corroborated by its recovered artwork, while the resource
identity itself does not depend on visual interpretation.

**Implementation:** `LastTurnEventPresentation` routes the three native
insufficient-cash report forms to already-loaded artwork index 6 (`PX06006`).

## Comlink

### BIN-COMLINK-001 - per-player message queue capacity and overflow

**Observation:** The Comlink recorder at `0x0045d2f0` stores one 166-byte record
in a player-strided table at `0x0049ca90`. The per-player count at
`0x004981e0 + player * 4` is compared with `0x10`. Counts below 16 append at the
current index. At capacity, the routine sets the index to 15, copies records
1 through 15 down into slots 0 through 14, writes the new record to slot 15,
and increments the count back to 16. It also decrements the player's visible
message cursor at `0x004981c8 + player * 4` when positive, otherwise retaining
zero.

All four direct callers have now been classified. Two calls in the legacy
transport dispatcher at `0x0046ba84` handle packet type 10 and pass either
message ID 0 or the received dynamic ID. The other two calls in the report
composition handler at `0x0045eab1` pass `-1`, which copies the already-formatted
global message buffer, and broadcast it to each enabled recipient. The recorder
itself sends packet type 10 for remote recipients. Consequently, Last Turn
Events filtering is unrelated to this capacity/overflow routine.

The composition handler loads `PX05018` (or alternate resource 5023), whose
template is labeled `COMLINK: SEND MESSAGE` and contains six recipient cells
plus four 40-character message rows. Its helper at `0x0045fdf1` renders six
recipient selectors and those four rows from offsets within the same 166-byte
buffer. The original Help independently states that Comlink View stores the 16
most recent messages sent by other human Overlords and that Send can target
multiple human recipients.

**Interpretation:** Each human player retains the newest 16 Comlink messages;
overflow deterministically drops exactly the oldest message and preserves the
viewer's logical position relative to the shifted entries. This bound does not
apply to the separate Last Turn Events panel.

**Confidence:** High static evidence from the complete bounded recorder,
literal capacity, record stride, copy bounds, count update, and cursor branch.

**Recreation status:** Authoritative local-human delivery now validates the
active command-phase sender and human recipients, supports deterministic
multi-recipient delivery, retains the newest 16 messages, and tracks unread
state. Version-23 saves, version-25 replays, and canonical hash version 26
include every inbox. The client routes the original `PX05017` View and
`PX05018` Send panels, including newest-first entry, paging, per-record read state,
six recipient cells, the four recovered 40-character rows, and the main-console
unread blink.

**Next validation:** Compare the routed panels against a native golden-screen
capture and recover the remaining legacy record fields; network transport
interoperability remains out of scope.

### BIN-COMLINK-002 - View navigation and hit geometry

**Observation:** View handler `0x0045d61a`, reached only from the left Comlink
half in main-console dispatcher `0x004718ee`, opens `PX05017` and copies it to
the standard panel destination. It reads the active player's count at
`0x004981e0 + player * 4`; before opening, it scans the 16 records at
`0x0049ca90 + player * 0xa60` in ascending storage order and selects the first
occupied record whose read byte is clear. If none is unread, it preserves the
existing cursor. An empty inbox plays general-effect slot 4 and returns without
opening the panel.

For a nonempty inbox, virtual keys Left (`0x25`) and Right (`0x27`) invoke the
bounded page helper, respectively refusing the first and last page with
slot 4. Enter (`0x0d`) and Execute (`0x2b`) invoke the standard pressed-control
helper over the dismiss rectangle. Pointer releases are first constrained to
the panel `(104,124)-(448,333)`, then translated to panel-local coordinates.
The half-open controls are Previous `(31,33)-(57,56)`, Next `(59,33)-(85,56)`,
and dismiss `(33,169)-(82,191)`. Each page change redraws the native dynamic
region `(104,124)-(448,333)` after rerendering the message. There is no Up,
Down, or Backspace navigation branch in this handler.

**Interpretation:** View starts at the oldest unread retained message, not
unconditionally at the newest record. Its arrows are 26-by-23 controls with a
two-pixel gap, and the 49-by-22 dismiss hit box is not the broader shared
command button inferred from nearby panels. The recreation now uses these exact
half-open bounds and the native Left/Right/Enter/Execute navigation set; its
global Escape/right-click panel return remains a documented modern navigation
convenience outside the original handler.

**Confidence:** High from the complete handler, its sole caller, literal
rectangle construction, virtual-key comparisons, cursor boundary branches, and
the already recovered Comlink record table.

**Recreation status:** Exact View hit geometry and keyboard navigation are
implemented and covered by `ComlinkUiTests`.

**Next validation:** Compare the rendered text/portrait fields and page-change
cadence with a native golden-screen capture; legacy record fields and transport
remain separate questions.

### BIN-COMLINK-003 - Send eligibility, controls, and composition cursor

**Observation:** Send handler `0x0045eab1`, the other Comlink branch from
`0x004718ee`, loads `PX05018` (or its alternate network template). It derives
six eligibility bytes from the enabled/player-controller arrays, clears the
active player, and refuses entry with general-effect slot 4 when no other human
recipient is eligible. Its six half-open, panel-local recipient controls are
`(98,20)-(198,52)`, `(98,54)-(198,86)`, `(98,88)-(198,120)`,
`(219,20)-(319,52)`, `(219,54)-(319,86)`, and `(219,88)-(319,120)`.
Eligible clicks toggle an independent selector byte; ineligible clicks reject.

The Cancel control is `(33,137)-(82,159)` and Send is `(33,169)-(82,191)`.
Both use shared held-button helper `0x00418821`: Cancel copies source
`PX00129 (50,409,50,23)` and Send copies `(50,386,50,23)` to a one-pixel-larger
50-by-23 destination while held inside, restores the baked face on pointer
exit, plays slot 3 on press, and activates only when released inside. Recipient
cards instead toggle directly after their hit test. Send with no selected
recipient rejects before entering the held-button helper.
Only virtual key Execute (`0x2b`) invokes Send; it rejects an empty recipient
selection. Enter (`0x0d`) sets the text cursor to column zero of the next row,
Backspace (`0x08`) deletes at the current cursor, and Left/Up/Right/Down move a
clamped four-row, 40-column cursor. Printable ASCII is uppercased before its
`0x20..0x5a` acceptance check. Cell writer `0x004600d2` paints each 6-by-7
glyph at `(199 + 6 * column,256 + 8 * row)`. Caret helper `0x0046023c` redraws
the focused cell from the ordinary `PX00129` glyph row at y=0 or the matching
inverse glyph row at y=441, using the same opaque surface-6 copy helper as its
panel artwork. Send begins with the normal row; each third consumed timer-0
event toggles this source row. The window-message callback
`0x004327c0` and direct signal helper `0x00432926` each raise one of the four
bytes at `0x00494810`; test/clear helpers `0x004328be`/`0x004328f8` make every
consumer observe a pending event once. The Send loop starts by signalling timer
0 and only advances its three-event counter when it consumes that flag.
Initialization routine `0x00460ccf` registers timer 0 through `0x004327dc` at
frequency 6; that helper calls `timeSetEvent(1000 / frequency, 20, ...)`, so
the timer period is integer `1000 / 6 = 166` ms and a glyph-row phase lasts
three timer events (498 ms).

**Interpretation:** Recipient selection is a 100-by-32 name-and-portrait target,
inset by one pixel within a 105-by-34 tile; it is not the narrower visual
portrait cell. Renderer `0x0045fdf1` paints every player card, including the
active player and computer/inactive cards, then uses the eligibility byte only
for its dimmed presentation and click rejection. Its backing color changes from
black to green when the selector byte is set. Enter is an editor navigation key,
not a message-submit shortcut. The original's composition is a fixed 4x40
editable grid rather than an append-only text field.

**Confidence:** High from the complete handler, literal rectangle construction,
eligibility/recipient loops, virtual-key switch, character-range branch, and
the `PX05018` resource call.

**Recreation status:** Send recipient hit testing now uses the exact wide native
rectangles, and only Execute can submit via keyboard. The text field is now the
recovered 4x40 overwrite grid: uppercase/range filtering, cursor-position
editing, Enter/arrow movement, horizontal wrap, row clamping, and Backspace
all follow the handler. Its normal glyph origin and 8-pixel row stride are also
exact. Recipient cards now use the recovered 105-by-34 backing/portrait/name
projection while eligibility remains authoritative. The managed cursor now
copies the recovered normal/inverse `PX00129` glyph cells and advances the
normal-to-inverse phase after each three 166-ms timer-zero events, without
making that presentation state part of a match. Cancel and Send now retain the
native pressed source sprites, release-inside action, and slot-3/slot-4 outcome
order; recipient selectors keep their direct toggle behavior.

**Next validation:** Compare the rendered caret phase, field, and recipient
tiles with a native golden-screen capture.

### BIN-COMLINK-004 - View record fields and projection

**Observation:** View projection helper `0x0045e04d` receives the active player
and retained-record count from `0x0045d61a`. It first sets byte `+1` of the
selected 166-byte record and then scans all 16 record read bytes to update the
global unread indicator. It copies the complete record from
`0x0049ca90 + player * 0xa60 + cursor * 0xa6` before formatting it. The copied
layout is:

| Offset | Size | Meaning | Evidence |
|---:|---:|---|---|
| `+0` | 1 | Occupied flag | View's unread-start scan tests it before byte `+1`; recorder appends/compacts complete records. |
| `+1` | 1 | Read/acknowledged flag | View sets it and all 16 rows are scanned for an unread record. |
| `+2` | 2 | Zero-based message turn | View formats `2050 + turn / 52` and `turn % 52 + 1`. |
| `+4` | 1 | Sender player slot | View indexes the 12-byte player-name table and active portrait/color tables with it. |
| `+5` | 160 | Message text | View copies four consecutive 40-byte rows and renders each separately. |
| `+165` | 1 | View-unconsumed tail | Completes the fixed 166-byte copy; it has no independent View consumer. |

The page header is rendered from the one-based selected record number and
record count. View selects the sender's fixed 10-character player name and
32-by-32 portrait/color record, then projects the four message rows. Its date
formula matches the city clock: record turn zero is `2050.01`.

**Interpretation:** The original Comlink record is a complete self-contained
view snapshot. It has no timestamp, recipient list, or wire packet ID in the
View-consumed portion; the only presentation state per record is its read bit.
The recreation's authoritative inbox already carries the equivalent occupied
queue position, read set, turn, sender, and bounded 160-character text rather
than exposing this native memory layout.

**Confidence:** High from the complete View renderer, the bounded recorder
`0x0045d2f0`, exact `0xa6` stride, four literal `0x28` row copies, and the
date/name/portrait table consumers.

**Recreation status:** Field semantics are represented and persisted in the
recreation-native Comlink inbox. The native in-memory arrangement remains
documentation only, as legacy save and transport interoperability are outside
scope.

**Next validation:** Golden-screen compare the View field locations, button
states, and palette treatment; no further static record-field recovery remains.

## Search

### BIN-SEARCH-001 - per-player site filters and city markers

**Observation:** Search handler `0x00448e32` loads `PX05024` and the 220x56
`PX00150` sheet. Its 22 row states live at `0x004a24e8 + player * 22`; ALL and
NONE write all 22 bytes, an individual row toggles one byte, and a row
double-click calls Site Information handler `0x0044c476` with the selected
definition. Fresh-game initialization at `0x0046e766` clears the complete
per-player table.

The sole city consumer at `0x00412990`, inside redraw routine `0x004123cc`,
walks the three physical site slots of each sector. A site controlled by the
active player is always visible. Any other site is visible only when its
definition's Search byte is set. Visible sites receive compact ordinals 0, 1,
and 2 and enter marker renderer `0x00412ac4`. That renderer copies a transparent
20x14 rectangle from `PX00150`: source x is `(definition % 11) * 20`, source y
is `(definition / 11) * 14 + (controlled ? 0 : 28)`. Its destination within
the 432x416 city buffer is x `(sector % 8) * 53 + 9` and y
`(sector / 8) * 51 + ordinal * 15 + 7`. The sheet's pure-white cell background
is the transparency key; it is not part of the white/gray controlled-site icon.

**Interpretation:** Search is a persistent per-player presentation filter for
individual city-site markers, not a sector highlight. Influence ownership is
the recreation's exact controlled-site predicate. With the overview filter
empty, only the active player's influenced sites appear using white/gray icons;
this includes the zero-resistance Headquarters in the player's starting sector,
which is controlled from the beginning without a separate Influence action and
contributes its `+2` Tolerance site effect.
Enabling site types through the 1.1 Search overview additionally reveals their
uninfluenced instances using amber icons, matching the 1.1 release notes. The
Search double-click shows definition-level information, including base rather
than live remaining Resistance.

**Confidence:** High static evidence from the complete handler, initializer,
sole selection-table consumer, and marker renderer, plus supplied original 1.1
city captures for marker colors and transparency.

**Recreation status:** The checklist now mutates an independent filter for each
player, draws the exact controlled/uncontrolled `PX00150` crops at recovered
city coordinates, compacts visible slots, and routes row double-clicks through
definition-level Site Information. Presentation filters reset on fresh match,
native-save load, and replay load because they are not authoritative state.

**Next validation:** Capture native Search and city golden screens to validate
palette transparency and pointer timing.

### BIN-SEARCH-002 - exact Search panel controls and row targets

**Observation:** Complete Search handler `0x00448e32`, reached from main-console
dispatcher `0x004718ee`, loads `PX05024`, initializes its display from the
active player's 22 bytes, and opens the standard panel. It treats these
panel-local rectangles as half-open:

| Control | Rectangle | Effect |
|---|---|---|
| ALL | `(33,16)-(82,39)` | Sets all 22 bytes to one, redraws the whole checklist. |
| NONE | `(33,48)-(82,71)` | Clears all 22 bytes, redraws the whole checklist. |
| Done | `(33,169)-(82,191)` | Uses the shared press/release helper and closes. |
| Site `n` | `(102 + 116*(n/11), 22 + 15*(n%11))` to `+ (114,15)` | Toggles just byte `n`; a double-click instead opens Site Information. |

ALL and NONE use distinct shared pressed-control sprites 4 and 5. The handler
constrains pointer input to `(104,124)-(448,333)` before translating it to
panel-local coordinates. Enter and Execute activate Done; right-click and all
other keyboard navigation behavior are not native Search-handler branches.

**Interpretation:** The previous recreation placed each row six pixels too far
left, four pixels too high, and made it one pixel too wide/short; it also used
an incorrect column/row pitch. The two native columns have a 116-pixel origin
stride and their rows have a 15-pixel pitch. ALL and NONE are 23 pixels high,
not generic 22-pixel command buttons.

**Confidence:** High from the complete handler, literal rectangle/point/size
arguments, per-byte mutation loop, direct double-click Site Information call,
and shared pressed-control call sites.

**Recreation status:** Search now uses the exact ALL/NONE and all 22 row hit
targets, with focused layout regression coverage. The recreation retains its
documented modern keyboard navigation; mouse behavior is native.

**Next validation:** Golden-screen and interaction captures for palette/cursor
treatment and click timing.

# Screens, panels, and options

Status: active clean-room research log
Last updated: 2026-09-20

Exact presentation facts recovered from draw and pointer call arguments: copy
modes and main-console geometry, the city and detailed-sector panels, the
alternate information panels and the number helpers they render through, the
title, credits, and menu resources, and the registry-backed options that change
what any of it does. [UI-ATLAS.md](../UI-ATLAS.md) maps the resources these
findings address.

Part of the [original executable internals research](../ORIGINAL-INTERNALS.md):
the [finding format](../ORIGINAL-INTERNALS.md#finding-format) defines the fields
every entry carries, the [finding index](../ORIGINAL-INTERNALS.md#finding-index)
lists every `BIN-*` ID across the set, and addresses are valid only for the
reference executable fingerprinted there.

<!-- doc-index:begin toc depth=3 -->
- [Rendering and console geometry](#rendering-and-console-geometry)
  - [BIN-UI-001 - combat animation cadence](#bin-ui-001---combat-animation-cadence)
  - [BIN-UI-031 - PX00129 uses role-specific copy modes](#bin-ui-031---px00129-uses-role-specific-copy-modes)
  - [BIN-UI-032 - exact main-console hit, split, and pressed geometry](#bin-ui-032---exact-main-console-hit-split-and-pressed-geometry)
  - [BIN-UI-034 - native pointer is stock-arrow/wait, not an atlas sprite](#bin-ui-034---native-pointer-is-stock-arrowwait-not-an-atlas-sprite)
- [City and sector presentation](#city-and-sector-presentation)
  - [BIN-UI-016 - Last Turn Events site-image treatment](#bin-ui-016---last-turn-events-site-image-treatment)
  - [BIN-UI-033 - exact Siege and Big Man objective-sector pylons](#bin-ui-033---exact-siege-and-big-man-objective-sector-pylons)
  - [BIN-UI-035 - sector Income and owner-only Cash rows](#bin-ui-035---sector-income-and-owner-only-cash-rows)
  - [BIN-UI-036 - detailed-sector site and gang meters](#bin-ui-036---detailed-sector-site-and-gang-meters)
  - [BIN-SECTOR-GANGS-001 - compact all-gangs sector roster](#bin-sector-gangs-001---compact-all-gangs-sector-roster)
- [Information panels](#information-panels)
  - [BIN-GAME-INFO-001 - alternate panel crop and field origins](#bin-game-info-001---alternate-panel-crop-and-field-origins)
  - [BIN-ITEM-INFO-001 - PX05001 alternate item-information panel](#bin-item-info-001---px05001-alternate-item-information-panel)
  - [BIN-SITE-INFO-001 - PX05002 alternate Site Information panel](#bin-site-info-001---px05002-alternate-site-information-panel)
  - [BIN-NUMBER-HELPERS-001 - baseline and modifier zero glyphs](#bin-number-helpers-001---baseline-and-modifier-zero-glyphs)
- [Title, credits, and menus](#title-credits-and-menus)
  - [BIN-UI-CREDITS-001 - blocking publisher/developer credits presenter](#bin-ui-credits-001---blocking-publisherdeveloper-credits-presenter)
  - [BIN-UI-MENU-001 - native menu resource and command groups](#bin-ui-menu-001---native-menu-resource-and-command-groups)
  - [BIN-UI-TITLE-001 - title canvas and dormant demo promotion](#bin-ui-title-001---title-canvas-and-dormant-demo-promotion)
- [Options and preferences](#options-and-preferences)
  - [BIN-OPTIONS-001 - registry keys, initialized defaults, and idle-gang warning](#bin-options-001---registry-keys-initialized-defaults-and-idle-gang-warning)
<!-- doc-index:end -->

## Rendering and console geometry

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

For phases 3 through 10, `FUN_00430c23` copies each 64-by-64 source frame into
backing-buffer rectangles `(left=150,top=274,right=214,bottom=338)` and
`(223,274,287,338)`. The shared panel occupies backing-buffer y=144..353 and is
then copied to screen `(104,124,448,333)`, producing exact screen animation
apertures `(254,254,64,64)` and `(327,254,64,64)`. These are inset within the
wider 67-by-64 action cells; stretching a frame across a complete cell erases
the embedded green dividers.

The detailed-combat setup in `FUN_0042e040` copies the sector tile into the
backing-buffer rectangle `(left=31,top=155,right=85,bottom=207)` and draws its
code at `(52,210)`. Relative to the panel's backing-buffer top of 144, those are
local tile bounds `(31,11,54,52)` and local text point `(52,66)`.

The native capture confirms 64-by-64 gang destinations at local `(150,48)` and
`(223,48)`. Beneath them, Force is represented by two matching 60-by-3 beveled
tracks at local y=114 and y=121. Each track uses light, full-intensity, and dark
rows rather than a flat fill.

**Interpretation:** Combat presentation advances at 6 Hz: 166 milliseconds per
frame in the original integer timer configuration, or about 1.33 seconds for
one eight-frame attack/hit clip. Damage removed from each force bar flashes
white twice before settling into the missing-force color, followed by a
five-tick result hold.
The recreation therefore keeps the action-cell bounds for panel structure but
draws and clears animation pixels only inside the two exact 64-by-64 apertures.

**Confidence:** High static evidence. The recovered timer setup, timer-slot use,
and frame-phase sequence agree; behavioral observation also identified the
previous recreation playback as too fast.

**Next validation:** Capture an original combat sequence with frame timestamps
to quantify any scheduling jitter around the 166-millisecond nominal period.

### BIN-UI-031 - PX00129 uses role-specific copy modes

**Observation:** Startup loads `PX00129` into surface 6 with
`FUN_00464108(6, 0x81, ...)`. Calls that use surface 6 do not share one alpha
policy. For example, `0x00413012` repeatedly calls
`FUN_00427864(6, 1, ..., 1)` for exact-white keyed UI elements, while
`0x00457b7b` copies the 32-by-32 Overlord portraits from source y 480 through
512 with mode 0 for inactive seats on the separate `PX00146` setup flow, then
uses ordinary opaque `FUN_0042773e` for active seats. Other surface-6 regions
likewise include opaque copies. Scaling through the wrapper is opaque
regardless of its requested mode.

A complete call census finds 502 direct calls to opaque `FUN_0042773e`: 501
push a literal source surface, including 131 surface-6 calls. Its sole
non-literal caller is one-second startup blit benchmark `0x00432954`, whose
only caller supplies surface 1 to surface 0. All 77 direct calls to
`FUN_00427864` push literal source surfaces; 66 use surface 6. Auditing the 26
functions containing those 66 calls classifies 64 as mode 1 and two as mode 0.
There is therefore no unaccounted parameterized surface-6 path through either
wrapper.

Gang-status compositor `0x00412bf7` builds a 20-by-20 source at x 492 and
y `67 + state * 20`, then uses `FUN_00427864(6, 2, ..., 1)` at both of its
copy sites. The assigned, idle, and incoming frames used by the recreation are
therefore exact-white keyed, not opaque. Setup drag helper `0x0040f72e` first
scales the selected 32-by-32 Overlord portrait opaquely into a 40-by-40 scratch
cell, then overlays source `(150,386)-(190,426)` from surface 6 with mode 1
before copying the composed token to the pointer. The static setup renderer at
`0x0040ee8a` supplies the second mode-0 request, but its 32-by-30 source is
scaled to 64 by 60, so the wrapper deliberately takes its opaque scaling path
and never applies the requested pattern. The twelve 20-by-20 active-player
frames at source y 626 likewise use opaque `FUN_0042773e`.

The marker state is a three-bit composition while the active player has a gang
in the sector: detectable opposing-player presence contributes 1, an active
gang with action byte zero contributes 2, and a pending hire targeting the
sector contributes 4. This selects all eight frames at y 67 through 207. A
pending hire without an existing friendly gang uses the ninth frame at y 227.
Presence builder `0x004123cc` clears the 64-by-6 table and sets an owner/sector
entry only for an active gang whose visibility byte for the current player is
nonzero, so an undetected enemy does not disclose itself by turning the marker
red. Atlas pixels use dark green `(0,156,0)` for the uncontested circle, dark
red `(156,0,0)` for its contested counterpart, yellow `(247,247,0)` for the
idle question mark, and brighter reds for the incoming-hire decoration.

Mode compositor `0x00427e60` selects 1-bit bitmap resource 147, 143, or 146.
The sole selector `0x00449b20` maps an input below 86 to resource 147, 86
through 170 to resource 143, and 171 or above to resource 146. Its raster
operations reduce to `(destination AND pattern) XOR (source AND NOT pattern)`:
a set pattern bit preserves the destination, while a clear bit copies the
source. In the unscaled `PX00146` inactive-seat path, fixed color `0x2661`
selects resource 146 before this stencil is applied.

The executable resources themselves are 8-by-8, 1-bit DIBs with a black/white
palette. Read as top-down rows, resource 143 alternates `0x55/0xaa` (32 set
bits), resource 146 alternates `0x88/0x22` (16 set bits), and resource 147
alternates `0xdd/0x77` (48 set bits). These exact masks can therefore be
reproduced without executing the original.

**Interpretation:** `PX00129` cannot be decoded into one globally transparent
texture. In particular, global white alpha deletes legitimate white font and
portrait pixels. The recreation now keeps an opaque atlas for fonts,
portraits, and opaque frames; a separate exact-white-keyed atlas for the
mapped `HIRED` stamp, sector-back arrow, gang-status markers, setup drag
frame, and objective-sector pylons. The recreation records and tests the exact resource-146 stencil but
does not apply it to local setup: that effect belongs to the unsupported
legacy `PX00146` flow, while local player-card scaling is opaque.

**Confidence:** High for the mixed native copy modes, portrait source
rectangle, status-marker and drag-frame roles, mode-0 Boolean operation,
selector bands, embedded masks, and complete source-surface call attribution;
Medium for semantic naming of every rectangle not yet used by the recreation.

**Next validation:** Name the remaining unused surface-6 rectangles and
pixel-compare the classified keyed/pattern roles when runtime captures are
permitted.

### BIN-UI-032 - exact main-console hit, split, and pressed geometry

**Observation:** Main-console dispatcher `0x004718ee` handles pointer input for
the city and detailed-sector screen states. It tests eight half-open outer
rectangles and passes cases 0 through 7 to pressed-control helper `0x00419022`:
Events `(500,126)-(548,174)`, Comlink `(552,126)-(600,174)`, Combat
`(500,178)-(548,226)`, Finance `(552,178)-(600,226)`, Gangs/Hire
`(500,230)-(548,278)`, Ranking/Search `(552,230)-(600,278)`, Done
`(500,282)-(600,330)`, and Game Info `(588,41)-(614,75)`.

The helper copies matching opaque `PX00129` pressed artwork from `(0,512)`,
`(48,512)`, `(96,512)`, `(144,512)`, `(192,512)`, and `(240,512)`, each
48 by 48; Done uses `(288,512,100,48)` and Game Info uses
`(190,386,26,34)`. It plays general-effect slot 2 once, restores the baked
surface-1 pixels whenever the pointer leaves the same outer rectangle, redraws
the pressed tile on re-entry, and succeeds only on release inside that tile.

The city/sector Hire input handler at `0x00416c75` divides the three 66-pixel
dock cells from x=438. Its Reject operation does not use the full right footer:
each release gate is `(top=437,left=472 + 66*slot,bottom=450,right=504 +
66*slot)`, i.e. a 32-by-13 half-open target. The broad portrait drag flow is
separate from this compact reject gate.

Five 48-pixel tiles select a subroute from the original press x coordinate,
not from separate vertical rows. Comlink, Combat, Finance, and Gangs/Hire use
the left 33 pixels and right 15 pixels: View/Send, Results/Detailed, City/Sector,
and Gangs/Hire respectively. Ranking/Search uses left 25 and right 23 pixels.
The strict comparisons are x greater than tile-left +32 or +24, so the boundary
pixel remains in the left route. Events, Done, and Game Info use their complete
outer rectangles.

**Interpretation:** The former recreation inferred 50-pixel rectangles from
the visible frame and split paired controls vertically. That both admitted
border pixels and routed large regions to the wrong action. The client now uses
the exact native tiles, horizontal subcontrols, original press identity,
release-inside cancellation, slot-2 press cue, and opaque pressed sprites on
both city and detailed-sector screens.

**Confidence:** High from the complete dispatcher, complete sole pressed-helper
switch and input loop, exact rectangle-constructor arguments, known branch
handlers, and matching `PX00128`/`PX00129` artwork.

**Next validation:** Remaining main-screen static work is content-layer and
cursor-role classification rather than console hit geometry.

### BIN-UI-034 - native pointer is stock-arrow/wait, not an atlas sprite

**Observation:** Window bootstrap `0x00465620` obtains its class cursor through
`LoadCursorA`; the executable contains no PX cursor resource path. Cursor helper
`0x00465bc8` caches its selected state and maps five selector values to stock
Windows cursor IDs: 0 is `IDC_ARROW` (`0x7f00`), 1 is `IDC_IBEAM` (`0x7f01`),
2 is `IDC_CROSS` (`0x7f03`), 3 is `IDC_NO` (`0x7f88`), and 4 is `IDC_WAIT`
(`0x7f02`). It loads the selected system handle and passes it directly to
`SetCursor`; there is no image decode, atlas copy, or custom hotspot involved.

The complete direct-call census contains 35 callers: all literal, with 20
selecting Arrow and 15 selecting Wait. No direct caller selects I-beam, Cross,
or No. The calls use the helper's force-update argument when changing into or
out of the synchronous original setup/load/resolve paths, so its state cache
does not suppress the transition. The three other switch branches are retained
code paths but have no direct caller in this executable.

**Interpretation:** Native interaction does not add cursor frames to `PX00129`
or any other extracted resource. The playable recreation's framework-managed
standard pointer correctly covers the reachable Arrow baseline and needs no
atlas-derived pointer, crosshair, text caret, or unavailable sprite. The native
Wait cursor is only visible around original synchronous work, whereas modern
asynchronous UI keeps its controls responsive and communicates pending work
in-screen.

**Confidence:** High from the complete import references, cursor-helper body,
all direct caller arguments, and absence of a custom-resource dataflow.

## City and sector presentation

### BIN-UI-016 - Last Turn Events site-image treatment

**Observation:** Both bitmap stretch wrappers, `FUN_0042773e` and
`FUN_00427864`, call `SetStretchBltMode(destinationDc, 3)` immediately before
`StretchBlt` (call sites `0x004277fd`/`0x00427854` and
`0x004279a2`/`0x004279f9`). Win32 mode 3 is `COLORONCOLOR`. In the site
cooperation branch of `FUN_0044fd6c`, the source rectangle starts at x 12 and
the site's 64-pixel atlas row plus 1, with width 94 and height 62. After it is
stretched into the 242-by-158 aperture, `FUN_004266a6` and `FUN_00427e60`
combine it through pattern-bitmap resource 146. That 8-by-8 monochrome pattern
retains two pixels per row at x 0/4 on even rows and x 2/6 on odd rows, making
75 percent of the background pixels black. Resource 6004 is composited after
this operation and is therefore unaffected by the pattern.

**Interpretation:** The original effect is a color-preserving nearest-style
stretch followed by a fixed 25-percent ordered-dither mask over a centered,
borderless portion of `PX02000`. Linear filtering without that mask is a modern
presentation option, not the native default.

**Confidence:** High static evidence; agrees with supplied original captures.

**Next validation:** Pixel-compare several native site-event captures with the
recovered crop, `COLORONCOLOR` projection, and resource-146 mask.

### BIN-UI-033 - exact Siege and Big Man objective-sector pylons

**Observation:** Complete city compositor `0x004123cc` first copies the neutral
map, replaces owned 52-by-50 interiors from the six ownership sheets, and then
tests gameplay scenario IDs 6 (**Siege**) and 8 (**Big Man**). For Siege it
compares every sector with the six dwords at `0x00494818`; initializer
`0x00439563` writes the fixed HQ candidates 9, 12, 30, 33, 51, and 54 there,
and setup permutation helper `0x00476726` assigns those same sectors to the six
players. For Big Man it tests literal sector IDs 27, 28, 35, and 36.

Both branches construct the same source rectangle with the executable's
top/left/bottom/right helper arguments `(15,344,67,398)`, which is ordinary
atlas rectangle `(344,15,54,52)`. The pixels are the two gray pylon structures
on exact-white background. Destination construction places the sprite over the
complete 54-by-52 city cell at `(4 + 53*column, 3 + 51*row)` on the map surface;
the later map placement adds screen offset `(2,44)`. Source and destination
dimensions match, so `FUN_00427864(6,2,...,1)` takes its unscaled mode-1 path
and removes exact white rather than falling through to an opaque stretch.

**Interpretation:** The pylons are one native objective marker shared by Siege
landmarks and Big Man's four scoring sectors. They are not procedural shapes,
and Big Man does not leave its special sectors visually unmarked. The
recreation now draws this exact keyed crop in both scenarios and removes its
former approximate Siege-only geometry.

**Confidence:** High from the complete renderer branches, fixed-sector table
writer and consumers, literal center-sector tests, rectangle-helper layout,
equal source/destination dimensions, keyed-copy mode, and visible atlas pixels.

**Next validation:** Golden-screen comparison remains useful corroboration;
the sprite identity, marked sector sets, copy mode, and placement are closed
statically.

### BIN-UI-035 - sector Income and owner-only Cash rows

**Observation:** The city and detailed-sector field renderer `0x004120ef`
reads the 36-byte sector record rooted at `0x004a08e8`. It renders offset `+4`
(`0x004a08ec`) on the **INCOME** row, offset `+5` on **TOLERANCE**, and offset
`+6` on **SUPPORT** only when the selected sector owner equals the active
player. Its final row reads offset `+3` (`0x004a08eb`) under the artwork's
literal **CASH** label, again returning zero for a non-owner.

The pre-planning sector rebuild helper `0x004782c5` preserves offset `+4`, sets
offset `+3` to the base sector tax of 1, and adds the Cash field of each
completed site. The later Upkeep scan in `0x0046e766`, lines 216-230, adds that
same signed offset-`+3` byte once to the owning player's cash and statistics.
The action-3 pass independently consumes offset `+4` for Chaos dice.

**Interpretation:** Income and Cash are distinct original fields. Income is the
generated 3-7 sector value used by Chaos and Control. Cash is the owner-visible
and owner-collected `1 + completed-site Cash` value. The shipped panel has no
sector-Chaos row; Chaos successes exist only in the whole-turn resolver's
temporary arrays.

**Confidence:** High static evidence from all four renderer reads, the
rebuild helper, the Upkeep owner scan, and the independent Chaos-pool read.

### BIN-UI-036 - detailed-sector site and gang meters

**Observation:** Detailed-sector compositor `0x00410770` calculates each site
percentage as integer `progress * 100 / base Resistance`, with the
zero-Resistance case fixed at 100. It copies exactly that many pixels from the
100-by-3 green strip at `PX00129` `(354,0)`, whose rows are light green
`(148,255,148)`, green `(0,247,0)`, and dark green `(0,140,0)`. The underlying
site frame supplies the matching 100-by-3 red track. Gang-card compositor
`0x00410130` copies `Force * 6` pixels from the same green strip over the
60-by-3 red track embedded in the card frame. The red rows are
`(255,148,148)`, `(247,0,0)`, and `(148,0,0)`.

The same compositor first copies the complete 74-by-110 card source
`(162,15)-(236,125)`, overlays the active action's 64-by-9 source at
`(162,125 + 9*action)`, places the 64-by-64 gang portrait at card offset
`(5,20)`, and places three optional 20-by-20 equipment portraits at offsets
`(5,86)`, `(27,86)`, and `(49,86)`. Caller `0x00410770` places cards at
`x = 254 + 76*(index % 2)` and `y = 80 + 112*(index / 2)`, then draws the
player-color outline one pixel outside the finished card.

The gang-card roster is friendly-only. Every ordinary caller passes the active
player at `0x004abc84` as `0x00410770`'s second argument. The compositor clears
six card-slot entries at `0x004abc68`, then scans only that player's 81 gang
records at `0x00498da8 + player * 0xa20`. It renders records whose active sector
byte at `+2` matches the selected sector and whose active-player visibility byte
at `+12 + activePlayer` is nonzero, preserving roster-slot order. Because the
scan never visits another owner's record block, detected enemy gangs do not
appear among the detailed-sector cards. Enemy visibility instead affects the
red contested status marker and the separate Attack picker roster described in
`BIN-DETECT-001`.

**Interpretation:** Both detailed-sector meters are three-row bevels copied at
native length. They are not flat fills, site progress is truncated rather than
rounded, and gang Force advances in exact six-pixel steps. The original draws
site progress only for the active sector owner. The recreation retains its
requested violet enemy-site progress as an intentional visibility extension,
but applies the same highlight/center/shadow structure. The detailed-sector
gang cards are not a visibility roster: they show at most six active-player
gangs in native roster order. A red marker or an Attack target can therefore
legitimately identify detectable opposition that is absent from these cards.

**Confidence:** High from the complete site and gang compositors, all ordinary
`0x00410770` call sites and their active-player argument, exact record-block and
visibility-byte addressing, source and destination rectangles, arithmetic
branches, and decoded `PX00129` pixels. The friendly-only roster was also
confirmed in an original 1.1 runtime observation.

### BIN-SECTOR-GANGS-001 - compact all-gangs sector roster

**Observation:** The `PX05009` Gangs in Sector handler at `0x0044e6ed` scans
all 81 roster records for active gangs in the selected sector. It advances one
display index for every match and draws each portrait into a 32-by-32 cell at
`(144 + 32*n,158)-(176 + 32*n,190)`. It then writes Tech Level, Upkeep, and
fourteen statistic values in that same column at x=`154 + 32*n`, with rows
192, 201, 211, 220, 229, 238, 248, 257, 266, 275, 284, 294, 303, 312, 321,
and 330. The six-gang sector capacity bounds the rendered columns to six.

**Interpretation:** `PX05009` is a simultaneous compact sector roster, not a
one-gang detail browser. The recreation renders every active gang in its
fixed-width card column while retaining keyboard selection as a non-destructive
quality-of-life shortcut for the city/sector workflow.

**Confidence:** High static evidence for roster scan, card dimensions/pitch,
all sixteen value rows, and ordering from `0x0044e6ed`; native capture remains
useful for confirming color treatment and over-cap corruption behavior.

## Information panels

### BIN-GAME-INFO-001 - alternate panel crop and field origins

**Observation:** Game Information handler `0x0045519d` loads `PX05021` into
the alternate backing area and its close transition copies source
`(344,144)-(664,353)` to final screen `(128,124)-(448,333)`. Its pointer
translation uses that 128-pixel destination left edge and tests the bottom face
as local `(33,169)-(82,191)`, or screen `(161,293)-(210,315)`. Dynamic text
is written at backing x=444 for the three header values, x=456 for player names, and right-aligned to backing x=624 for intelligence; these map to screen x=228, x=240, and right edge 408. Header y values map to 151, 169, and 187, while the six player rows map to y=214 + 9*n.

The renderer's roster pass is fixed, not player-count bounded: it iterates
`n = 0..5`, reads each 12-byte name record from `0x004a2589 + 12*n`, then
draws a status label for that same slot. The match-start setup screen likewise
copies all six configured name records before play. The recreation's local
bootstrap therefore correctly completes every unclaimed slot as a computer
player before this panel is available, instead of omitting rows for seats that
were not manually configured. The input-copy helper `0x0040f63d` stores at
most ten characters, filters its modal text buffer to the original printable
range `0x20..0x5a`, and terminates the 12-byte record. The reachable native
text-entry path supplies upper-case text; modern online display names
consequently project deterministically into that same ten-character record
before they enter match state, while the lobby may still show its complete
modern name.

The first header is scenario-sensitive. For scenario ids 0 through 3 (Greed,
Power, Acceptance, and Dominance), the renderer appends the two-byte literal
opening ` (` from `0x00487768`, selects resource ids `0x36` through `0x39`
from the stored duration values 26, 52, 104, and 208, then appends the closing
`)`. The same duration strings used by Setup decode as `6 MONTHS`, `1 YEAR`,
`2 YEARS`, and `4 YEARS`. Scenario ids 4 through 9 write the scenario string
alone.

The second and third headers each select an independent four-string resource
table. The AI-mentality byte selects ids `0x2e` through `0x31`, which decode as
`GOON`, `CRIMINAL`, `CRIME LORD`, and `HOMICIDAL MANIAC`. The planning-limit
byte selects ids `0x32` through `0x35`, which decode as `NONE`, `30 SECONDS`,
`2 MINUTES`, and `5 MINUTES`. Direct extraction with `LoadLibraryEx` as a data
file and `LoadStringW` from the shipped executable confirms both tables; the
duration table is ids `0x36` through `0x39`. The six player-status labels that
share the next resource block begin `HUMAN`, `AI`, and `ELIMINATED` at ids
`0x3a`, `0x3b`, and `0x3c` respectively. In each player row, the renderer
tests the eliminated-state byte first and selects `ELIMINATED` when it is set;
only an active row then selects `AI` or `HUMAN` from the controller byte.

**Interpretation:** `PX05021` is a 320-by-209 alternate panel, not the normal
344-pixel shared template. Its source crop must be retained when drawing the
imported panel rather than stretching it, and all dynamic x origins move 24
pixels right from the former shared-template approximation. Consequently Game
Information must display, for example, `GREED (6 MONTHS)` but just `SIEGE`.
The independent planning-limit table happens to use the same visible labels as
the setup control, so `PlanningTimerPolicy.Label` is presentation-compatible
with the native Game Information field without conflating its stored byte with
the duration selector. An eliminated seat must likewise display `ELIMINATED`,
regardless of whether its original controller was human or AI.

**Confidence:** High static evidence for alternate source/destination rectangles, close target, field origins, alignment, and row stride from complete handler `0x0045519d`, plus direct resource extraction for all three header tables; native capture remains useful for palette and text clipping.

### BIN-ITEM-INFO-001 - `PX05001` alternate item-information panel

**Observation:** Item Information handler `0x0044b699` loads `PX05001` into
the alternate backing area and closes through the same 320-pixel crop as Game
Information: backing `(344,144)-(664,353)` reaches final screen
`(128,124)-(448,333)`. The item monitor copies the current 48-by-48 cell from
its loaded `PX04xxx` 720-by-48 strip to backing `(378,161)-(426,209)`, or
screen `(162,141)-(210,189)`. The frame counter starts at zero and advances
through 0..14 before wrapping.

Identification starts at backing `(444,171)`, with the type right-aligned to
backing x=624. Three description buffers of exactly 30 bytes are written at
backing x=444, y=189, 198, and 207. Cost and Tech Level use two-cell numeric
fields at backing x=516 and x=612, y=236; their fourteen statistic fields use
those same x positions at screen rows 243, 252, 270, 279, 288, 297, and 306.
Those coordinates map to screen identification `(228,151)`, type right edge
408, description rows y=169/178/187, and numeric field origins x=300/396.
The sole activation control is the standard bottom face at local
`(33,169)-(82,191)`: Enter/Execute and a successful pointer press close the
panel, while an outside press takes the rejected-input path.

**Interpretation:** `PX05001` is an alternate 320-by-209 panel, not the
344-by-209 shared panel. Its 30-byte description rows and shifted origins are
part of the native layout; using the shared panel's 29-column description and
x=199 identification origin overwrites the template labels and causes the
identification/description overlap.

**Static follow-through:** Item Information now keeps the item's three
authored fixed 30-character rows instead of word-reflowing them. For example,
METAL PIPE retains `TITANIUM ALLOY.`, `GOOD FOR BUSTING IN A FEW`, and `HARD
HEADS.` on its three native screen rows. Its numeric clears remain exactly two
glyph cells wide: extending the clear into a speculative third cell overwrites
the template's right border. The dynamic-value clear stops before that frame;
the original panel's source pixels remain the sole owner of its edge.

**Numeric helper detail:** Cost and Tech Level use `0x00414187`; the fourteen
item modifier fields use `0x004142e7`, with every call passing literal width
two. For a nonzero negative, either helper negates the value and selects the
red numeric row of surface 6 (source y=8), then copies each absolute-value
digit from the same fixed two-cell walk. Neither selects an ASCII minus glyph
or allocates a third cell: `-2` is a right-aligned red `2`, and the table's
`-12` values fill the two red cells with `12`. Unlike `0x00414187`, the
modifier helper copies the dim-green zero glyph at source `(354,8)` for an
exact zero. The recreation follows both bounded presentations.

**Confidence:** High from complete renderer and input handler `0x0044b699`,
including literal backing/destination rectangles, text destinations, fixed
numeric helper widths, frame-wrap branch, and exit control.

### BIN-SITE-INFO-001 - `PX05002` alternate Site Information panel

**Observation:** Site Information handler `0x0044c476` loads `PX05002` into
surface 7 at backing `(344,144)-(688,353)`, then uses the alternate 320-pixel
slide crop. Its final screen rectangle is `(128,124)-(448,333)`. The site
portrait is copied from backing `(372,159)-(492,223)`, yielding screen
`(156,139)-(276,203)`. The title is written at backing `(504,171)`, or screen
`(288,151)`. Resistance, Tolerance, Support, and Cash use the two-cell helper
at backing x=612, screen x=396, on rows 169, 187, 196, and 205. The fourteen
statistics use screen x=300/396 and y=244, 253, 271, 280, 289, 298, and 307.
The sole close face is the shared alternate target `(161,293)-(210,315)`.

**Interpretation:** `PX05002` is not a shared 344-pixel panel. Every dynamic
Site Information field shifts right into the alternate crop, and its signed
values use the same bounded two-cell numeric helper as Item and Gang
Information.

**Static follow-through:** The recreation now renders/clicks `PX05002` through
the alternate geometry, aligns tooltip hit regions with the shifted labels,
and shares the native two-cell numeric presentation with Gang and sector-roster
fields.

**Confidence:** High from the complete renderer/input handler `0x0044c476`,
including resource destination, portrait/title/numeric coordinates, all
literal helper calls, and close branch.

### BIN-NUMBER-HELPERS-001 - baseline and modifier zero glyphs

**Observation:** `0x00414187` and `0x004142e7` both walk exactly the supplied
number of six-by-seven cells, right-align nonzero decimal digits, and choose
the red digit row for a negative absolute value. The former receives a final
"show leading zeros" flag; every inspected panel call passes false, so zero is
the ordinary bright green `0` in the final cell. The latter has no flag and
takes a separate zero branch: it copies a blank for every leading cell and
then copies source `(354,8)-(360,15)`. Direct PX00129 inspection shows that
cell is the same zero shape at the dim green intensity (14/31) rather than the
bright green intensity (26/31) or red negative row.

**Panel mapping:** Gang and Gang Definition use the baseline helper for Force,
Upkeep, Tech Level, Combat, Defence, Stealth, and Detect, then the modifier
helper for the ten Command Skills. Hire and Gangs in Sector use the baseline
helper for their first six Tech/Upkeep/basic-stat rows and the modifier helper
for the remaining ten rows. Item Information uses baseline Cost/Tech fields
and modifier fields for all fourteen effects. Site Information uses the
modifier helper for every numeric field.

**Static follow-through:** The recreation now carries the helper kind to the
renderer. Bright baseline zeroes and dim modifier zeroes are distinct while
remaining fixed at two glyph cells; nonzero and negative values retain the
already recovered placement and red absolute-digit behavior.

**Confidence:** High from both complete helper bodies, their direct caller
inventory, complete panel renderers, and pixel inspection of the three source
glyph cells. Native capture remains useful only to verify blend/palette output.

## Title, credits, and menus

### BIN-UI-CREDITS-001 - blocking publisher/developer credits presenter

**Observation:** The native message dispatcher `0x00462579` calls
`0x00464d53` only for control event `(0x80, 3)`. The executable's RT_MENU
resource 101 gives that event a visible source: its final Help submenu item is
`&About Chaos Overlords...`, marked `MF_END` (`0x0080`) and assigned command
`0x8003`. The presenter first removes the normal screen surfaces, loads
resource 100 (`PX00100`) into surface 3 with the full `(640,460)` size, and
copies it opaquely across the complete canvas. Its local message loop exits
for the normal mouse/key/window termination messages (2, 3, 4, 17, and 18);
paint messages redraw the same full-canvas surface. On exit it reloads surface
3 with resource 3000 and restores the previous screen-surface ownership.

**Interpretation:** The publisher/developer image is a dedicated blocking
credits screen, not a title-background layer or a game-state panel. Its entry
is the native **Help → About Chaos Overlords...** menu command, distinct from
the title canvas.

**Confidence:** High for the resource, visible command label/ID, full-canvas
composition, blocking lifetime, and redraw path from the RT_MENU bytes, sole
dispatcher branch, and complete presenter. The native menu's screen geometry
remains unobserved.

**Recreation status:** `PX00100` remains extracted but is deliberately not
routed until the native Help menu itself is recovered. Adding an invented title
button would be a recreation-only UI change rather than a faithful port.

### BIN-UI-MENU-001 - native menu resource and command groups

**Observation:** RT_MENU resource 101 defines the original application menu.
The command dispatcher represents each 16-bit menu ID as its high and low
bytes, so `0x8003` reaches the `(0x80,3)` branch above. Its complete authored
menu is:

- **File:** New Game `0x8101`, Open `0x8102`, Save `0x8103`, End `0x8104`,
  Host `0x8106`, Join `0x8107`, and Exit `0x8109`.
- **Options:** Thousands of Colors `0x8401`, Full Screen `0x840b`, Music
  levels `0x0601..0x060b`, Sound Effects `0x0701..0x070b`, Base Statistics
  `0x8406`, Detailed Combat `0x8407`, Slide Panels `0x8408`, and Warn if Idle
  Gangs `0x8409`.
- **Comm:** Disconnect `0x8501`; legacy None `0x8503`, WinSock `0x8504`,
  Modem `0x8505`, and Direct Connect `0x8506`.
- **Help:** Help Topics `0x8001` and About Chaos Overlords `0x8003`.

**Interpretation:** The original window owns a conventional menu bar in
addition to its 640-by-460 art canvas. File, Options, and Help semantics are
not title-screen buttons, while Comm is solely a legacy transport selector.
The menu proves that credits are globally reachable from every supported
screen, but it does not alter the explicit decision not to carry legacy
WinSock/modem/direct-connect transports forward.

**Confidence:** High from the complete RT_MENU 101 template and the dispatcher
branch. Menu-bar pixel geometry and focus behavior remain native-runtime work.

**Recreation status:** Current title, game-menu, Options, and F1 Help routes
cover the supported actions semantically. The recreation has no native menu-bar
surface, so About/Credits remains deliberately unrouted rather than appearing
as a falsely original title control. Modern public lobby and private join-key
online flows remain the sole supported multiplayer routes.

### BIN-UI-TITLE-001 - title canvas and dormant demo promotion

**Observation:** The title loop at `0x00460ccf` repeatedly loads `PX00130`
into surface 1 at its full 640-by-460 extent, then opaque-copies that surface
to the display before entering the title routes. The same sequence appears on
initial entry and on returns from local setup, legacy-network setup, and load.
The decoded sheet contains the Chaos Overlords logo and copyright title canvas.

`PX00131` is also a full 640-by-460 sheet, but its only renderer is the
dedicated blocking presenter at `0x004653de`. That presenter draws the sheet,
restores it after system messages, and exits for close-event types `2`, `4`,
`6`, or `18`. The decoded text is a limited/demo-version sales promotion. A
complete direct-reference census finds no caller for that presenter in the
supported 1.1 executable, unlike the live `PX00130` title loop.

**Interpretation:** `PX00130` is the live title background. `PX00131` is
retained demo-build promotional content, not a normal full-version first-run
splash or title interaction.

**Confidence:** High. The resource loads, display copies, decoded sheets, and
the complete direct-call census agree.

**Recreation status:** The recreation uses `PX00130` for its title background.
It deliberately does not route `PX00131`; adding a new route for a presenter
with no direct caller in the supported executable would invent behavior.

## Options and preferences

### BIN-OPTIONS-001 - registry keys, initialized defaults, and idle-gang warning

**Observation:** Loader `0x0046439a` opens
`HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` once with access mask
`0x20019` (`KEY_READ`) and queries thirteen four-byte values in this order:

| Registry value | Destination | Compiled default |
|---|---:|---:|
| `prefsVidDeep` | `0x00487844` | 1 |
| `prefsSlide` | `0x00487840` | 1 |
| `prefsBaseStats` | `0x0048784c` | 0 |
| `prefsCombat` | `0x0048785c` | 1 |
| `prefsFreeGang` | `0x00487860` | 1 |
| `commType` | `0x00487884` | 0 |
| `prefsVolumeSFX` | `0x00487864` | 6 |
| `prefsVolumeCD` | `0x00487868` | 5 |
| `prefsDiff` | `0x00487850` | 1 |
| `prefsTimeLimit` | `0x00487854` | 0 |
| `prefsObjective` | `0x00487858` | 0 |
| `prefsFullScreen` | `0x0048786c` | 1 |
| `serialNum` | `0x00487870` | 0 |

All twelve preference fields are written as `REG_DWORD` by `0x00464783`, but
that routine opens the same HKLM key with the same `KEY_READ` mask. It never
requests `KEY_SET_VALUE`, and ignores every `RegSetValueExA` result. Its three
callers are in the application flow at `0x00460ef9`, `0x00460f40`, and
`0x0046224a`; none can persist a change through this handle. The loader likewise
ignores every query result and reuses one DWORD without resetting it between
names. A missing or malformed later value can therefore consume stale data from
the preceding query instead of retaining its compiled default. When the final
effective serial is zero it consumes two bounded RNG calls and attempts to write
the generated value through the same read-only handle, so that write also fails.
An installer or external tool may still have pre-populated the machine-wide
values before launch.

Reads of the Slide byte occur directly in the panel-open and panel-close
functions at `0x0041953e` and `0x004196f5`; the Base Stats byte is consumed by
gang/statistics presentation paths; and the Combat byte is read by the
Done/combat path at `0x0046fd80`. That same city handler scans all 81 gang slots
on Done; an active slot whose action byte is zero is idle. When `prefsFreeGang`
is enabled and such a slot belongs to the active player, the path invokes the
two-choice modal at `0x00448718`. Its open and close paths use the panel-slide
functions above, which play general-effect slots 0 and 1. The original Help
independently says Done warns about gangs without commands unless Warn If Idle
Gangs is off.

**Interpretation:** The original defaults are Slide Panels on, Current rather
than Base gang statistics, Detailed Combat on, and Warn If Idle Gangs on.
The warning is a confirmation boundary around finishing planning, not a
simulation rule; continuing still permits unassigned gangs.

**Confidence:** High from the initialized data, complete ordered load/save API
calls and access masks, all three writer callers, direct consumer references,
bounded Done-path scan, dialog call graph, and matching Help description.

**Recreation status:** Options uses the recovered defaults and persists its
warning toggle, base/current gang-stat display, automatic Detailed Combat
playback, and bounded panel motion. The legacy 16-bit color choice is displayed
as always enabled by the modern renderer. Recreation-native global F11 switching
between windowed and borderless-fullscreen display is persisted in preference
version 6 without changing compatibility coordinates. Version-4 and version-5
preferences migrate without losing their earlier selections and safely default
the new display choice to windowed mode.
The safe local store deliberately does not reproduce the original read-only-HKLM
writer or cross-value stale-buffer behavior; see `DECISIONS.md`.
Finishing planning checks only the active player's living gangs and offers a
Continue/Go Back modal when any lacks a queued command. Opening and closing the
modal route the recovered general-effect slots 0 and 1.

The panel's wording is baked into `PX05020`: `SYSTEM WARNING:`, `IDLE GANG
DETECTED`, and `AT LEAST ONE OF YOUR GANGS HAS NOTHING TO DO. ARE YOU SURE YOU
WANT TO END YOUR TURN?` The Cancel button is above OK. Handler `0x00448718`
loads resource 5020, draws it at `(104,124,344,209)`, and hit-tests panel-local
Cancel `(33,137)-(82,159)` before OK `(33,169)-(82,191)` using Win32-exclusive
right/bottom bounds. Keyboard event type 2 confirms on virtual key `0x0d`
(Enter) or legacy `0x2b` (`VK_EXECUTE`) and cancels only on `0x1b` (Escape).
It contains no Y, N, or Backspace shortcut. The recreation now follows those
bindings and exact pointer rectangles; its missing-asset fallback reproduces
the complete baked wording.

**Confidence:** High from the complete handler, exact instruction comparisons,
rectangle-helper layout, `PtInRect` wrapper, and decoded `PX05020` pixels.

#### Panel-slide geometry and speed calibration

The panel-open function at `0x0041953e` and reverse close function at
`0x004196f5` copy a 344-by-209 panel horizontally from or toward the right
edge. The rectangle helper at `0x00425edf` packs its arguments as
`(top, left, bottom, right)`, confirmed by the `BitBlt` coordinate extraction
at `0x0042773e`; this also matches the decoded `PX050xx` dimensions. Their
primary form travels 344 pixels from source-buffer x=0 into screen x=104..448.
Its destination is exactly `(top=124,left=104,bottom=333,right=448)`. The
recreation now uses that native destination through one shared panel-local
coordinate system: background, baked-field clearing, dynamic content, buttons,
and hit regions all derive from origin `(104,124)`. This atomic migration avoids
the exposed placeholder strokes and displaced borders caused by moving only the
background while leaving descendants on the older y=125 basis.
An alternate form reads a 320-pixel source region beginning at buffer x=344
and moves it toward the same right-edge destination. Exhaustive literal-argument
classification identifies six open/close caller pairs using that form:
`0x0044b699` loads Item Information `PX05001`, `0x0044c476` loads Site
Information `PX05002`, `0x0044d1bb` loads City/Sector Financial
`PX05008`/`PX05019`, `0x004546c5` loads Hire comparison `PX05016`,
`0x0045519d` loads Game Info `PX05021`, and `0x00455b6b` loads Gang Definition
Information `PX05022`. The other seventeen caller pairs pass zero and use the primary
344-pixel form. The Hire handler makes the distinction explicit: it first loads
the 344-by-209 image into `(top=144,left=344,bottom=353,right=688)`, then
calls the nonzero slide mode. That mode copies only source x=344..664 to screen
x=128..448, so the final panel is the same 320-pixel alternate crop as the
other five callers rather than a shared-panel presentation.
Both calculate a step from the startup blit benchmark at `0x00432954`. That
benchmark counts identical copies for just over one second. The transition
divides the count by four, divides its travel by that result, and clamps the
step to at least 16 pixels. The intended uncapped duration is therefore about
one quarter second, while the minimum step prevents excessive intermediate
copies on faster hardware. Slide enabled plays general-effect slot 0 before
opening and slot 1 before closing; disabled mode skips intermediate copies and
still presents the final state.

The recreation now uses a bounded 250 ms time-based horizontal entrance over
the recovered per-screen 344- or 320-pixel travel. It intentionally avoids the
original startup-speed dependency. Runtime capture must still validate close
timing/interruption behavior.

#### Planning timer

The same preference block also initializes byte `0x00487854`, corresponding to
`prefsTimeLimit`, to zero. New-match initialization at `0x0046e766` maps values
0, 1, 2, and 3 to `-1` (disabled), 30,000 ms, 120,000 ms, and 300,000 ms.
The original Game Settings Help independently describes None, 30 seconds,
2 minutes, and 5 minutes and says the limit exists to constrain slow turns in
multiplayer games. The four controls occupy the right-hand rows alongside AI
Mentality in `PX00143`.

For a human planning entry, `0x0046fd80` calls the start helper at `0x0041b8bc`,
which records `timeGetTime`; computer planning skips it. The expiry helper at
`0x0041bdd5` returns true once elapsed milliseconds exceed the selected limit,
and the human loop then exits as if Done had been accepted. This check occurs
after the user-triggered idle-gang confirmation path, so timer expiry does not
open that confirmation. The drawing helper at `0x0041b8fc` first computes
integer elapsed percent as `(elapsedMilliseconds * 100) / limitMilliseconds`,
then computes visible width as `60 - (elapsedPercent * 60) / 100`; both
divisions truncate. This percent-first quantization differs from scaling the
remaining duration directly. It calls general slot 7 while remaining time is
strictly between 1 and 10 seconds and slot 8 while remaining time is greater
than zero and at most 1 second. In the input pump at `0x00462579`, counter
`0x00487898` is decremented before comparison; a zero calls the helper and
resets the counter to 6. Timer drawing and warning checks therefore recur every
sixth eligible pump call. The sound wrapper at `0x00464290` forwards every call
to the low-level player at `0x0045851a`; it has no timer-specific suppression.
Exact wall-clock cadence still depends on the original pump rate and needs a
controlled capture. In the supported asset pack, those two PCM clips last
approximately 0.117 and 1.189 seconds.

**Recreation status:** Setup exposes the four original choices at the original
hit regions and safely persists the selection, defaulting to None. A bounded
presentation-only timer starts only after the automatic private Game
Information/Combat/Events route has returned the player to the planning city;
it remains active through player-opened planning panels, renders the original
percent-quantized 60-by-3 aperture, and checks the recovered warning slots
every sixth fixed update. Expiry submits the normal replay-recorded
finish-planning operation and deliberately bypasses the idle-gang confirmation.
The timer itself is absent from Core state,
state hashes, snapshots, and replay payloads; only its resulting ordinary
operation is authoritative.

**Next validation:** Capture the original wall-clock warning cadence,
deactivation behavior, and whether modal dialogs perceptibly pause the timer.

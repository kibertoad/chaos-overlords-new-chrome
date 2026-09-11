# Original UI atlas

Status: partial, active mapping
Last updated: 2026-09-11

This file assigns presentation resources from the verified extracted asset pack
to visible workflows. Coordinates use the original 640 by 460 virtual canvas.
Mappings based only on image inspection are Provisional until a controlled
original-game capture confirms the screen and interaction state.

## Full-screen resources

| Resource | Mapping | Confidence | Recreation use |
|---|---|---|---|
| `PX00100` | Publisher/developer credits | High from visible text | Not yet routed |
| `PX00128` | Main city view and right control-panel frame | High from visible labels | City screen background |
| `PX00130` | Chaos Overlords title/logo | High from visible title | Title screen background |
| `PX00131` | Limited/demo-version promotion | High from visible text | Not used for full version |
| `PX00143` | Full local objective/player setup | High from visible labels and load at `0x0040e150` | Setup screen background |
| `PX00144` | Alternate full objective/player setup flow | Medium from visible layout and isolated load at `0x00467b06` | Unsupported legacy setup flow |
| `PX00145` | Compact player Add/Remove/Begin/Cancel setup flow | Medium-High from visible layout and isolated load at `0x0040bc2e` | Unsupported legacy setup flow |
| `PX00146` | Minimal player-strip Begin/Cancel setup flow | Medium from visible layout and isolated load at `0x00457295` | Unsupported legacy setup flow |

## Composite sheets and panels

| Resource | Mapping | Confidence |
|---|---|---|
| `PX00129` | Main UI composite sheet: original font strip `(0,0,354,7)` containing six-pixel ASCII cells from space through `Z`, action names, player bars, arrows, buttons, portraits, message controls and command icons; diagonal `HIRED` stamp `(120,300,60,60)`; assigned `(492,67,20,20)`, idle/question `(492,107,20,20)`, and incoming-hire `(492,147,20,20)` gang-status markers | High from pixel inspection, visible content, and user captures; remaining rectangles not yet complete |
| `PX00132` | Next-player/Ready handoff panel | High from visible labels |
| `PX00137`, `PX00139` | Empty and filled horizontal meter frames | Medium |
| `PX00138` | Circular action/command icons | High from repeated command imagery |
| `PX00140` | Compact setup-control sheet matching `PX00143` labels | High |
| `PX00150` | Two-state small command/equipment icon sheet | Medium |
| `PX00200` | Endgame awards/statistics frame | High from visible labels |
| `PX00201` | Endgame award/statistics symbols and controls | High from visible labels |
| `PX00202`, `PX00203` | Victory and elimination panels | High from visible text |
| `PX00300` | Police portrait, weapon, patrol car, donut and header sprites; patrol-car cell `(116,0,48,64)` | High for sheet contents, Medium for patrol-car crop |
| `PX05008`, `PX05019` | City Financial and Sector Financial panels sharing account rows for upkeep, contracts, equipment, officials, tax, protection, estimated Chaos and adjustment | High from visible labels and original WinHelp Finance topic |
| `PX05020` | System Warning panel for confirming an end turn while at least one active gang is idle | High from visible text and client trigger semantics |
| `PX05021` | Scenario Information panel: objective, global AI mentality, turn time limit, six color-coded player name/intelligence rows, and OK control | High from visible labels and original WinHelp Game Info topic |
| `PX05022` | Gang Information variant without live-instance equipment cells, used for hire-offer definition inspection | High from comparison with `PX05000` and Hire/Gang help topics |
| `PX02000` | 22 vertically stacked site portraits, 120x64 each | High from dimensions and definition coverage |
| `PX03000` | 10x9 gang portrait grid, 64x64 each, covering all 90 definitions | High from dimensions and definition coverage |
| `PX07000`-`PX07027`, `PX07200`-`PX07228` | Eight-frame 64x64 attacker overlays facing opposite directions; index 27 is target-evasion/question art and right-facing index 28 is the police car | High from frame inspection and item-table indices |
| `PX07100`-`PX07119`, `PX07300`-`PX07320` | Eight-frame 64x64 hit/background layers facing opposite directions; right-facing index 20 is the police beam impact | High from composited frame inspection and item-table indices |
| `PX05000`-`PX05024` | Gang-information panel family | Medium from visible template fields |
| `PX10000`-`PX10006` | Neutral plus six player-colored 8x8 city layers; each sector is a 54x52 source cell | High from dimensions, grid, and color inspection |

## `PX00143` provisional hit map

The recreation currently overlays selection borders and routes clicks through
these rectangles:

- Objectives: two columns at x 80 and 192, width 108; row tops 102, 137,
  171, 206 and 241 with heights 30-31. Row-major order follows the ten
  `ScenarioId` values.
- Durations: x 80, 136, 192 and 248 at y 282, heights 24 and widths 50-52.
- AI Mentality: x 80 with width 108; row tops 330, 359, 388 and 417,
  each 27 pixels high.
- Planning time: x 192 with width 108; row tops 330, 359, 388 and 417,
  each 27 pixels high, corresponding to None, 30 Seconds, 2 Minutes and
  5 Minutes.
- Add player: `(370,328,92,24)`.
- Remove player: `(468,328,92,24)`.
- Begin: `(370,375,92,45)`.
- Cancel: `(468,375,92,45)`.

The four push-button rectangles are verified against the destination rectangles
in original helper `0x0040eb5f`; the other coordinates were measured from the
extracted bitmap. The helper uses half-open rectangle containment, restores the
released image when the pointer leaves, and accepts only release inside. The
recreation now uses its exact hit rectangles, defers each action until release
inside the same control, and cancels a release outside. While held inside, it
draws the helper's exact `PX00140` source tiles: Add `(220,0,92,24)`, Remove
`(220,24,92,24)`, Begin `(220,48,92,45)`, and Cancel `(220,93,92,45)`; moving
outside restores the baked `PX00143` control. Disabled rendering and original
cursor feedback remain to be validated.

## Rendering rules recovered so far

- All mapped screens render in a 640 by 460 virtual canvas.
- Scaling uses point sampling and centered letterboxing.
- Mouse coordinates are inverse-mapped through the same scale/offset as drawing.
- Extracted RGB555 bitmaps are loaded from the installed asset pack; they are
  not embedded in source or redistributed.
- `PX00129` contains sixteen 32-by-32 overlord portraits across source row
  y=480. Setup renders these in the top strip and scales the selected portrait
  into each visible local-player slot; green left/right arrows cycle that
  player's portrait. Static setup analysis now identifies portrait 15 as the
  empty-slot marker, not an active Overlord portrait. On original local Begin,
  every empty slot becomes a Computer and receives a unique bounded draw from
  portraits 0 through 14 before city generation. The client now treats its
  visible count as explicitly configured local humans, starts with one, and
  completes omitted slots at Begin through the original-compatible fresh-match
  factory. Add/Remove changes that human count. Clicking the bounded name field
  below a visible face opens the original 10-character uppercase editor; empty
  confirmation restores `PLAYER#n`. Dragging a face to an empty cell changes its
  color slot; dropping on another human exchanges their name/portrait identities.
  Portrait 15 remains display-only.
- AI difficulty is the setup screen's single global **AI Mentality** selection,
  not a per-player field. The four baked rows select Goon, Criminal, Crime Lord,
  or Homicidal Maniac; hover-only thematic tooltips explain the behavioral
  emphasis and avoid claiming hidden resources; static analysis confirms and
  the simulation implements mentality-dependent command-resolution calibration.
- The optional human planning countdown uses the 60-by-3 aperture at
  `(520,336)` on the main control panel. The recreation fills it green over a
  black background and scales the visible width from 60 to zero.
- The `PX00128` Game Info button at `(588,40,30,54)` opens `PX05021` at
  `(104,125,344,209)`. Dynamic fields report the scenario, global AI mentality,
  selected planning limit, and all six names with the manual-defined `HUMAN` or
  `AI` intelligence label. The panel opens automatically for a new game with
  multiple local humans and after loading a live saved game.
- The idle-gang end-turn check now uses the baked `PX05020` System Warning at
  `(104,125,344,209)`. Its original Cancel control returns to planning and its
  OK control confirms the ordinary end-turn path.
- The split Financial City/Sector control selects `PX05008` or `PX05019` at
  `(104,125,344,209)`. Both render the active Overlord portrait and a read-only
  projection of current/pending upkeep, contracts and headcount, equipment,
  bribes, tax, influenced-site cash, estimated Chaos and the resulting cash
  adjustment. Costs are red and income is green as specified by the manual.
- City sectors use their fixed 54 by 52 cell from `PX10000` when neutral or
  `PX10001` through `PX10006` according to owner, composited at `(2,44)`.
- Active Crackdown sectors overlay the color-keyed patrol-car slice from
  `PX00300`; pure black is treated as transparent for this sprite sheet.
- Sector detail uses `PX02000` source `(0, siteId*64, 120, 64)`; gang detail
  and hiring use `PX03000` source `((gangId%10)*64, (gangId/10)*64, 64, 64)`.
- Sector detail also shows up to ten friendly or detected enemy portraits from
  `PX03000` in owner colors. Friendly portraits are mouse-selectable and open
  that gang's information panel; enemy portraits remain read-only.
- The main control panel's Gangs/Sector half selects only active friendly gangs
  in the current sector, refuses an empty roster, and keeps arrow navigation
  within that stable ID-ordered roster. Live gang details use `PX05000` and fill
  its three right-side weapon/armor/miscellaneous cells from `PX04999`; a hire
  offer has no instance equipment and therefore uses the clean `PX05022` form.
- Search results reuse the same sheet and visibility projection for seven
  portrait-led rows, with owner-colored borders and overflow count.
- The Equipment panel shows the selected gang from `PX03000` at `(558,58)` in
  a 56-by-56 owner-colored frame, keeping the item list and statistics visible.
- Gang Information places its 64-by-64 portrait at `(67,90)`, aligned to the
  inner aperture of the `PX05000` template drawn at `(42,74)`.
- The city Hire dock uses three 66-pixel cells beginning at `(438,370)`, with
  64-by-64 `PX03000` portraits at x 439, 505, and 571. Dragging an available
  portrait shows a 36-by-36 token and highlights valid controlled-sector drops;
  a reserved recruit retains its cell under the color-keyed original `HIRED`
  stamp from `PX00129`. Available candidates show their two-digit initial hire
  price with a minimum width of two digits (`06`, but `11` remains `11`),
  centered in the left 33-by-24 footer half at y 436;
  the reject control
  occupies the right half. This interaction and placement were confirmed in the
  user-supplied original-game capture and footage at 03:57.
- Each sector containing the active player's gangs displays the original
  idle/question or assigned 20-by-20 status marker. The whole-city map projects every sector containing
  an active gang (assigned wins when a sector contains mixed command states),
  matching the detailed-sector minimap. A pending or actively dragged hire uses the red-edged
  incoming marker at the prospective sector; incoming state wins if markers
  overlap. All three mappings were confirmed against user-supplied captures.
- Whole-city and detailed-sector views are distinct. The detailed Sector screen
  preserves the shared overlord strip at y=0, starts its selected-sector content
  at y=48, and renders the selected sector at the center of a native-size 3-by-3 crop of the
  `PX10000`-`PX10006` ownership layers, clips neighbors at the city boundary,
  and overlays coordinate labels, police, and gang-status art. Its three site
  slots use the matching 120-by-64 strips from `PX02000`; clicking a neighboring
  tile recenters the detail view.
- Dragging a friendly gang portrait past the same four-pixel threshold used by
  Hire creates a 36-by-28 scaled-art token and highlights only validator-legal
  neighboring minimap sectors. Dropping queues a one-off Move command;
  stationary clicks retain gang selection and double-click inspection.
- Hire dragging remains active over the detailed-sector screen. A drop on its
  workspace reserves the recruit for the centered sector, while a drop on the
  visible minimap uses the indicated controlled sector. The drag token and
  destination feedback render above the sector-detail layer.
- Combat Summary places compact owner-colored attacker and defender portraits
  beside each visible attack result; police rows use the `PX00300` patrol car
  opposite the attacked gang.
- Resolved combat plays the item-selected `PX070xx`+`PX071xx` eight-frame pair;
  retaliation uses the mirrored `PX072xx`+`PX073xx` pair. The presentation also
  routes the recovered question/evasion and police-car/beam sheets. Weapon IDs
  used by both sides are retained in the combat event so later elimination or
  equipment changes cannot alter sound or animation selection. Static analysis
  establishes a 6 Hz presentation timer for these strips: each frame remains
  visible for 166 ms, making a complete eight-frame clip about 1.33 seconds.
  After the strip finishes, the force lost by the struck gang alternates as a
  white segment twice before the bar settles to green remaining force and red
  missing force; the recovered timer state machine also retains the result for
  five final ticks.
- The recreation Give target panel reuses `PX03000` portraits and lists only
  validator-approved friendly recipients in the acting gang's sector. Its
  layout remains provisional pending identification of the original panel.
- `PX05004` and `PX05007` are the original Equipment to Purchase and Equipment
  to Research overlays. Equip and Research route legal item choices through
  these panels over the live detailed-sector view. Both open on the first of
  four left-side tabs: melee (Strength and Blade), ranged, armor, and
  miscellaneous; the list contains only legal items in the selected category.
  Equipment is not expanded
  into separate entries in the top-level action menu.
- `PX05000` is the gang-information overlay. Double-clicking an owned gang card
  or a stationary Hire-dock portrait opens it over the current view. Hire art is
  not promoted to a drag token until the pointer moves beyond the click
  threshold, preserving double-click inspection. Unhired gang information shows
  Force as `??`; the authoritative health value is instantiated on hire.

## `PX00128` provisional control routes

The city frame currently routes Events `(492,124,50,51)`, Combat Summary
`(492,176,50,49)`, and Financial `(548,176,50,49)`. The lower compact rows are
Gangs `(492,226,50,17)`, Hire `(492,260,50,17)`, Sector `(548,226,50,17)`,
Ranking `(548,243,50,17)`, and Search `(548,260,50,17)`. These rectangles come
from bitmap inspection and remain provisional until executable capture confirms
their exact edges and pressed states.
Done uses the original panel cell at `(492,278,106,54)` in the recreation;
normal mode resolves the internal phases automatically, while `--debug-phases`
retains explicit advancement.
Single-clicking a whole-city sector selects it; a second click on the same
sector within 500 ms opens the detailed Sector view. It does not queue a gang
command. Enter remains the keyboard command shortcut.

The detailed view follows the original full-screen composition from the
reference capture: the 3-by-3 neighborhood begins at `(61,48)`, three 120-by-64
site portraits stack at `(83,226)`, and up to two visible gang cards begin at
`(251,80)`. Framed coordinate badges overlap the neighborhood edges. The live
right console, shared top portrait strip, and three-offer Hire dock remain
visible; only dynamic values are painted over the console's baked labels.
Each friendly card exposes separate one-off and repeating order controls; both
use the authoritative legal-command picker and set the existing
`GameCommand.Repeat` flag appropriately. The repeating control uses the
reference's vertically stacked double-arrow mark. The thin track above the
portrait is red, filled green in proportion to current Force. Once an order is assigned, the
two arrow cells are replaced by one full-width strip naming the queued action.
Dragging an owned gang card onto an influenceable building in the detailed
sector queues a recurring Influence command for that exact site.
Hovering a gang with an assigned Move, Influence, or Attack command outlines
its destination tile, building portrait, or target gang card respectively.
The gang-card frame ends at the bottom of its three equipment slots; the next
eight rows in `PX00000` are separate command-arrow artwork and must not be
included in the card source rectangle.
The first themed overlay preserves the original fifteen-action ordering:
Attack, Bribe, Chaos, Control, Equip, Give, Heal, Hide, Influence, Move,
Research, Sell, Snitch, None, and Terminate. Individual equipment and other
targets appear only in a second target overlay, never as top-level actions.
The Attack target-acquisition roster includes only detectable enemy gangs in
the acting gang's sector and groups them by ascending player slot. Selecting an
opponent portrait displays that player's eligible gangs simultaneously in a
three-column, two-row grid; clicking a gang selects that exact target and the
active green OK button confirms it. Raw global gang-id ordering and click-to-
cycle behavior are not used.
Each detailed-sector building has a red control track filled green in proportion
to reduced resistance; the starting Headquarters is fully green while neutral
buildings begin red. A building already influenced by an opponent uses purple
instead of green for its completed control track, while its portrait border
continues to identify the influencing player's color.

`PX05016` is the original 344-by-209 `GANGS FOR HIRE` comparison panel. The
Hire console button overlays it on the live city, with three 32-by-32 gang
portraits and their sixteen comparison values. Hiring itself remains the
original drag-from-dock interaction; the comparison panel's OK control closes
the overlay.

`PX05017` is the original 344-by-209 `COMLINK: INCOMING MESSAGES` viewer. It
contains the bounded page counter and previous/next controls, a 64-by-64 sender
portrait, date and sender fields, and the message aperture. `PX05018` is the
matching `COMLINK: SEND MESSAGE` panel: six recipient cells in two columns by
three rows and four fixed 40-character composition rows. The recreation routes
both halves of the main-console Comlink control, blinks View while the active
human has unread mail, and uses authoritative inbox/read/send operations.

`PX05001` is the shared Item Information panel opened from the Equip and
Research item lists. `PX05003` is the `TARGET ACQUISITION` Attack picker: it
shows the acting gang, an opponent-player portrait column, and the selected
enemy gang with its equipment and Force track. `PX05005` is the `SITE TO
INFLUENCE` picker; its three staggered apertures contain the selected sector's
actual building art. These identities and workflows are confirmed by supplied
original-game captures. The recreation implements `PX05005` and the two-stage
player/gang selection of `PX05003`. `PX05001` is now shared by Purchase and
Research: a stationary item-row double-click opens its art, type, description,
cost, tech level and fourteen modifiers, then returns to the same tab/selection.

`PX05002` is the Site Information panel. Its 120-by-64 aperture uses the same
`PX02000` strip as the detailed-sector buildings; the right data block reports
live remaining Resistance plus the site's Tolerance, Support and Cash, and the
lower block reports all fourteen site modifiers. A stationary double-click
opens it from either a detailed-sector building or a `PX05005` Influence target,
then returns to the originating screen without discarding target selection.

`PX05014` is the dedicated live Combat comparison panel rather than a flat
target list. It identifies the sector, places attacker and defender owner/gang
art side by side, shows equipment and green/red Force tracks, and reserves the
mirrored lower-center cells for the recovered attack and hit animations.
`PX05012` is the separate paged Combat Results panel behind the right-console
Combat Summary control. It pages the viewer's combat notifications, identifies
the affected sector, and presents both sides with gang/equipment art and Force
tracks; police uses the recovered police art. Its Detail control replays the
selected resolved event through `PX05014`, regardless of the automatic Detailed
Combat preference. Escape or the panel's Cancel control clears the bounded
presentation queue without touching match state. Both identities are confirmed by
their template text, apertures, and the supplied original Combat capture.

`PX05010` is the paged Last Turn Events panel. At the next human-player handoff,
the recreation opens it automatically when that player has queued notifications
and otherwise proceeds directly to the city. Its counter and arrow cells page
one report at a time. Its `(198,133,242,158)` aperture uses the dedicated
`PX06001`-`PX06009` report illustrations (`PX06002` is sector control attained),
not stretched city tiles or gang portraits; the two footer lines report
date/object and status.
Routine implementation notifications such as upkeep/economy, movement,
equipment transactions and ordinary command completion do not create reports;
captured/lost control, newly influenced sites, completed research, crackdowns,
eliminations and objective changes do. Closing the panel consumes the queued
notifications that were present when it opened.

## Next mapping work

1. Identify the main-city content layers placed inside the black viewport of
   `PX00128` and validate/complete its provisional right-panel button rectangles.
2. Finish classifying the separate `PX00144` through `PX00146` setup flows;
   implementing legacy network/setup protocols remains explicitly out of scope.
3. Map remaining cursor frames, selection/pressed-state sprites and transparency.
4. Capture reference screenshots for title, every setup configuration and the
   initial city, then add masked native-resolution golden comparisons.

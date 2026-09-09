# Original UI atlas

Status: partial, active mapping
Last updated: 2026-09-09

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
| `PX00143` | Six-slot new-game objective/player setup | High from visible labels | Setup screen background |
| `PX00144` | Setup variant with reduced/changed player area | Low | Unmapped |
| `PX00145` | Compact player setup frame | Low | Unmapped |
| `PX00146` | Minimal two-slot setup frame | Low | Unmapped |

## Composite sheets and panels

| Resource | Mapping | Confidence |
|---|---|---|
| `PX00129` | Main UI composite sheet: glyphs, action names, player bars, arrows, buttons, portraits, message controls and command icons | High from visible content; rectangles not yet complete |
| `PX00132` | Next-player/Ready handoff panel | High from visible labels |
| `PX00137`, `PX00139` | Empty and filled horizontal meter frames | Medium |
| `PX00138` | Circular action/command icons | High from repeated command imagery |
| `PX00140` | Compact setup-control sheet matching `PX00143` labels | High |
| `PX00150` | Two-state small command/equipment icon sheet | Medium |
| `PX00200` | Endgame awards/statistics frame | High from visible labels |
| `PX00201` | Endgame award/statistics symbols and controls | High from visible labels |
| `PX00202`, `PX00203` | Victory and elimination panels | High from visible text |
| `PX00300` | Police unit/equipment header sprites | High from visible content |
| `PX03000` | Gang portrait/sprite composite | High from visible content |
| `PX05000`-`PX05024` | Gang-information panel family | Medium from visible template fields |
| `PX10000`-`PX10006` | Neutral plus six player-colored 8x8 city layers; each sector is a 54x52 source cell | High from dimensions, grid, and color inspection |

## `PX00143` provisional hit map

The recreation currently overlays selection borders and routes clicks through
these rectangles:

- Objectives: two columns at x 80 and 192, width 108; row tops 102, 137,
  171, 206 and 241 with heights 30-31. Row-major order follows the ten
  `ScenarioId` values.
- Durations: x 80, 136, 192 and 248 at y 282, heights 24 and widths 50-52.
- Add player: `(370,326,92,30)`.
- Remove player: `(466,326,96,30)`.
- Begin: `(370,374,92,50)`.
- Cancel: `(466,374,96,50)`.

These coordinates were measured from the extracted bitmap. Exact inclusive
edges, pressed states, disabled states and original cursor feedback remain to be
validated against the executable.

## Rendering rules recovered so far

- All mapped screens render in a 640 by 460 virtual canvas.
- Scaling uses point sampling and centered letterboxing.
- Mouse coordinates are inverse-mapped through the same scale/offset as drawing.
- Extracted RGB555 bitmaps are loaded from the installed asset pack; they are
  not embedded in source or redistributed.
- City sectors use their fixed 54 by 52 cell from `PX10000` when neutral or
  `PX10001` through `PX10006` according to owner, composited at `(2,44)`.

## `PX00128` provisional control routes

The city frame currently routes Events `(492,124,50,51)`, Financial
`(548,176,50,49)`, Gangs `(492,226,50,49)`, Sector `(548,226,50,49)`, and
Ranking `(548,276,50,49)`. These rectangles come from bitmap inspection and
remain provisional until executable capture confirms their exact edges and
pressed states.

## Next mapping work

1. Identify the main-city content layers placed inside the black viewport of
   `PX00128` and validate/complete its provisional right-panel button rectangles.
2. Correlate `PX00143` through `PX00146` with local/network player counts.
3. Map fonts, cursor frames, selection/pressed-state sprites and transparency.
4. Capture reference screenshots for title, every setup configuration and the
   initial city, then add masked native-resolution golden comparisons.

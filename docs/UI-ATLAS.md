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

## Next mapping work

1. Identify the main-city content layers placed inside the black viewport of
   `PX00128` and map its right-panel button rectangles.
2. Correlate `PX00143` through `PX00146` with local/network player counts.
3. Map fonts, cursor frames, selection/pressed-state sprites and transparency.
4. Capture reference screenshots for title, every setup configuration and the
   initial city, then add masked native-resolution golden comparisons.

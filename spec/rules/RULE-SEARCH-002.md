---
id: RULE-SEARCH-002
title: The city shows a marker for each site the viewer controls and for each other site of a type the viewer's Search filter selects
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SEARCH-003, FND-SEARCH-001, FND-UI-036, FND-GANG-001]
conflicting: []
split_with: []
related: [FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

The city map marks sites with small icons. A site the player controls always
gets a white and grey icon. Another site gets an amber icon only when the
player has selected its type in the Search panel. Up to three icons stack in
each sector, with no gaps for the sites left out.

## When it runs

Each time the city map is drawn.

## Parameters

None.

## Inputs

`active_player`, `sectors`, `search_filters`, `site_definitions`.

## Procedure

```text
define site_controlled(s, slot) -> UINT8:
    let site = sectors[s].sites[slot]
    return sectors[s].owner == active_player and site.progress == site_definitions[site.definition].resistance

for s in 0..64:
    let ordinal = 0
    for slot in 0..3:
        let site = sectors[s].sites[slot]
        let controlled = site_controlled(s, slot)
        if controlled or search_filters[active_player * 22 + site.definition]:
            let source_x = (site.definition % 11) * 20
            let source_y = (site.definition / 11) * 14
            if not controlled:
                source_y = source_y + 28
            let x = (s % 8) * 53 + 9
            let y = (s / 8) * 51 + ordinal * 15 + 7
            emit SiteMarkerDrawn(site.definition, controlled, source_x, source_y, x, y)
            ordinal = ordinal + 1
```

## Outputs

No return value. Emits one `SiteMarkerDrawn` for each marker: the 20-by-14
rectangle at (`source_x`, `source_y`) of `PX00150`, copied without its pure
white background to (`x`, `y`) in the 432-by-416 city buffer.

## Edge cases

- With an empty filter, only controlled sites are marked, including the
  Headquarters in a player's starting sector once it counts as controlled.
- The first drawn site of a sector sits at y offset 7, the second at 22 and the
  third at 37, whatever slots they occupy.

## What the sources say

SRC-MANUAL-GOG does not describe the Search panel or the site markers.

## Differences between builds

None known.

## Open questions

- The test the renderer uses for "controlled by the active player" is not
  recorded. `site_controlled` writes it as a completed site in a sector the
  active player owns, following the glossary's `site` entry; the renderer's
  own test is still to be read.
- The order in which the sectors are visited is not recorded; it does not
  change what is drawn.

---
id: RULE-GFX-003
title: A keyed image copy leaves out the pixels of maximum white
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-PLATFORM-015, FND-GFX-001, FND-GFX-007, FND-GFX-009, FND-PLATFORM-014, FND-AWARDS-004]
conflicting: []
split_with: []
related: [FMT-GFX-001, RULE-GFX-002]
---

## Summary

The copy wrapper has a keyed mode in which every source pixel of maximum white
is left out and every other pixel is copied unchanged. 72 of the wrapper's 77
calls ask for it: the interface sheet's frames, markers, stamps and tabs, the
city renderer's site markers, one Last Turn illustration, the setup screen's
arrow overlay [FND-PLATFORM-015], the Search panel's site icons and the
network lobbies' seat overlay [FND-GFX-009] among them. Runs of the original
show the selected-sector frame and the grid's edge tabs drawn through it
[FND-PLATFORM-014].

## When it runs

Whenever wrapper `0x00427864` is called with mode 1 for an unscaled copy. A
scaled copy ignores the mode and is opaque. The mode-1 calls are listed in
FND-PLATFORM-015: 64 of them copy cells of the interface sheet `PX00129`
(FND-UI-031), and 8 copy from surface 7, which holds `PX00150` for the site
markers and the Search panel's icons (FND-GFX-009), `PX06004` for the Last
Turn illustration, `PX00140` for the arrow overlay of the setup screen and of
the two network lobbies' seat cards (FND-GFX-009), and `PX00201` for the award
icons when they are drawn (FND-AWARDS-004).

## Parameters

- `source`: `UINT16[]` at 16-bit depth or `UINT8[]` at 8-bit depth, the
  pixels of the source rectangle as the surface holds them.
- `destination`: the pixels of the destination rectangle, of the same size and
  depth.

## Inputs

None beyond the parameters.

## Procedure

```text
# 16-bit depth: the key RGB(255,252,255) names the RGB555 value 0x7FFF
for p in 0..count(source):
    if source[p] != 0x7FFF:
        destination[p] = source[p]
```

At 8-bit depth the key is `RGB(255,255,255)` and the pixels whose colour is
that white are left out in the same way. The compositor picks the key by the
display depth and sets it with its first `SetBkColor` call; its second call
restores the colour the device context had [FND-PLATFORM-015].

## Outputs

Changes the destination pixels whose source pixel is not the key. Changes no
game state.

## Edge cases

- Black and every near-white colour are copied; only the one maximum white
  is the key.
- Of the interface sheet's 85,137 white pixels, every one a copy reads lies in
  a cell copied with the key, except one pixel of the fifth Overlord portrait,
  which is copied opaquely and drawn white [FND-GFX-007].
- Each of the 22 Search icon cells of `PX00150` holds exact white, and the
  arrow overlay of `PX00140` is exact white everywhere but its two arrows, so
  those copies show what lies beneath their white pixels [FND-GFX-009].
- No shipped `PX16` pixel has bit 15 set (FND-GFX-001), so every pixel the
  copy compares is a plain RGB555 colour.
- The 16-bit key names `0x7FFF` only on a 16-bit surface. On a 32-bit desktop
  the surfaces hold that white as `RGB(255,255,255)`, the key matches nothing,
  and the copy is opaque, so the white is drawn [FND-PLATFORM-014].

## What the sources say

None of the sources describes the image copies.

## Differences between builds

None known.

## Open questions

- How a display driver converts the 16-bit key at the boundary between 5-bit
  and 8-bit channels is decided at run time and has not been observed
  (FND-PLATFORM-015).
- No run at 8-bit depth has been made, so the 8-bit key rests on the static
  reading alone.
- Whether the network dispatcher can reach the lobby seat functions after the
  lobby handler has returned, or the Site Information panel opened from a
  Search row writes the top rows of surface 7, either with surface 7 holding
  another image, has not been traced (FND-GFX-009).

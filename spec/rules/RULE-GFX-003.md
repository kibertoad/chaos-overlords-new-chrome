---
id: RULE-GFX-003
title: A keyed image copy leaves out the pixels of maximum white
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-PLATFORM-008, FND-GFX-001, FND-PLATFORM-014]
conflicting: []
split_with: []
related: [FMT-GFX-001, RULE-GFX-002]
---

## Summary

The copy wrapper has a keyed mode in which every source pixel of maximum white
is left out and every other pixel is copied unchanged. Only two images are
drawn this way: the city renderer's site markers and one Last Turn
illustration.

## When it runs

Whenever wrapper `0x00427864` is called with mode 1 for an unscaled copy. Its
constant mode 1 callers are the site marker copy at `0x00412AC4` inside
`0x004123CC`, and the Last Turn illustration copy inside `0x0044FD6C`
(FND-PLATFORM-008). A scaled copy ignores the mode and is opaque.

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
that white are left out in the same way.

## Outputs

Changes the destination pixels whose source pixel is not the key. Changes no
game state.

## Edge cases

- Black and every near-white colour are copied; only the one maximum white
  is the key.
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
  (FND-PLATFORM-008).

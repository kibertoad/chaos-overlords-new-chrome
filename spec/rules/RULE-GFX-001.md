---
id: RULE-GFX-001
title: Decoding the RLE8 pixel data of a PX08 image
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GFX-002, FND-GFX-003, FND-PLATFORM-002, FND-GFX-008]
conflicting: []
split_with: []
related: [FMT-GFX-002]
---

## Summary

Most 8-bit images store their pixels run-length encoded in the standard
Windows RLE8 scheme. This rule turns that data into pixel values, one byte per
pixel, line by line from the bottom of the image.

## When it runs

Whenever the image loader uploads a `PX08` file whose `compression` is 1
(FMT-GFX-002). The executable passes the data to `SetDIBits` with the
compression unchanged, so Windows performs the decoding (FND-PLATFORM-002).

## Parameters

- `data`: `UINT8[]`, the `rle8_data` bytes of the file.
- `width`: `INT32`, the image width the executable supplies.
- `height`: `INT32`, the image height the executable supplies.

## Inputs

None beyond the parameters.

## Procedure

```text
let image: UINT8[] = []
for p in 0..width * height:
    append(image, 0)
let x = 0
let y = 0
let i = 0
while i + 1 < count(data):
    let n = data[i]
    let v = data[i + 1]
    i = i + 2
    if n > 0:
        # encoded run: n copies of the value v
        for k in 0..n:
            if x < width and y < height:
                image[y * width + x] = v
            x = x + 1
    else if v == 0:
        # end of line
        x = 0
        y = y + 1
    else if v == 1:
        # end of bitmap
        return image
    else if v == 2:
        # delta: move right and up without writing
        x = x + data[i]
        y = y + data[i + 1]
        i = i + 2
    else:
        # absolute run: the next v bytes are pixel values, padded to an even count
        for k in 0..v:
            if x < width and y < height:
                image[y * width + x] = data[i + k]
            x = x + 1
        i = i + v + (v & 1)
return image
```

## Outputs

Returns `image: UINT8[]` of `width * height` pixel values. Element
`y * width + x` is pixel `x` of line `y`, where line 0 is the bottom line of
the picture. Changes no game state.

## Edge cases

- Every shipped `rle8_data` block ends with the end-of-bitmap code on its last
  two bytes, never uses the delta code, and writes exactly `width` pixels on
  each of its `height` lines (FND-GFX-002, FND-GFX-003). The guards against
  writing outside the image, the delta branch and the pixels left at 0 are
  therefore never reached by the game's own files.
- `SetDIBits` decodes every shipped file to the pixels this procedure gives,
  and on hand-written data it cuts a run at the end of its line, follows a
  delta code, and accepts data without the end-of-bitmap code, as the
  procedure does (FND-GFX-008).

## What the sources say

None of the sources describes the image files' compression.

## Differences between builds

None known.

## Open questions

- A pixel no code writes keeps what the target bitmap held before
  `SetDIBits` (FND-GFX-008). The procedure gives it 0, which holds only when
  the original's temporary bitmap (FND-PLATFORM-002) starts at 0, and how
  that bitmap is created has not been read. No shipped file leaves a pixel
  unwritten, so the game's images do not depend on it, and the entry stays
  `supported` until it is settled.

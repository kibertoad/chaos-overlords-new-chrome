---
id: RULE-GFX-001
title: Decoding the RLE8 pixel data of a PX08 image
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GFX-002, FND-GFX-003, FND-PLATFORM-002]
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

## What the sources say

None of the sources describes the image files' compression.

## Differences between builds

None known.

## Open questions

- Windows decodes the data, not the game. What `SetDIBits` does with a pixel
  that no code writes, with a run past the end of a line, or with a delta code
  is written here as the plain BMP definition reads, and has not been observed.
  None of it matters for the shipped files.

meta:
  id: fmt_gfx_002
  title: 8-bit image files in DATA/PX08
  license: MIT
  endian: le
  imports:
    - fmt_gfx_003
doc: |
  A Chaos Overlords PX08 image: an 8-bit Windows bitmap file with width and
  height set to 0 and planes set to 255, a 256-entry colour table, and pixel
  data that is RLE8-compressed (compression 1) or stored as plain rows
  (compression 0). The executable supplies width, height and planes itself.
doc-ref: FMT-GFX-002, FND-GFX-002, FND-GFX-003, FND-PLATFORM-002
seq:
  - id: signature
    contents: [0x42, 0x4d]
    doc: BM.
  - id: file_size
    type: u4
    doc: The file's size in bytes.
  - id: reserved_06
    type: u2
    doc: 0.
  - id: reserved_08
    type: u2
    doc: 0.
  - id: pixel_offset
    type: u4
    doc: Offset of the pixel data, always 1078.
  - id: header_size
    type: u4
    doc: Size of the information header, always 40.
  - id: width
    type: s4
    doc: 0 in every file; the executable replaces it with its caller's width.
    doc-ref: FND-PLATFORM-002
  - id: height
    type: s4
    doc: 0 in every file; the executable replaces it with its caller's height.
    doc-ref: FND-PLATFORM-002
  - id: planes
    type: u2
    doc: 255 in every file; the executable replaces it with 1.
  - id: bit_count
    type: u2
    doc: Bits per pixel, always 8.
  - id: compression
    type: u4
    enum: compression
    doc: How the pixel data is stored.
  - id: image_size
    type: u4
    doc: Size of the pixel data in bytes, the file size minus 1078.
  - id: x_pixels_per_meter
    type: s4
    doc: 2835.
  - id: y_pixels_per_meter
    type: s4
    doc: 2835.
  - id: colors_used
    type: u4
    doc: 0, meaning all 256 colour table entries.
  - id: colors_important
    type: u4
    doc: 0.
  - id: palette
    type: fmt_gfx_003
    repeat: expr
    repeat-expr: 256
    doc: The colour of each pixel value.
    doc-ref: FMT-GFX-003
  - id: rle8_data
    size: image_size
    process: rule_gfx_001
    if: compression == compression::bmp_compression_rle8
    doc: |
      RLE8 pixel data, decoded by RULE-GFX-001 into height rows of width pixel
      values, bottom row first. Width and height come from FMT-GFX-001's
      Coverage table.
    doc-ref: RULE-GFX-001, FND-GFX-002
  - id: raw_pixels
    size: image_size
    if: compression == compression::bmp_compression_none
    doc: height rows of width pixel values, bottom row first, each padded to a multiple of 4 bytes.
    doc-ref: FND-GFX-002, FND-GFX-003
enums:
  compression:
    0:
      id: bmp_compression_none
      doc: Plain rows.
    1:
      id: bmp_compression_rle8
      doc: BMP RLE8.

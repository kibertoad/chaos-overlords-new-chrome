meta:
  id: fmt_gfx_001
  title: 16-bit image files in DATA/PX16
  license: MIT
  endian: le
doc: |
  A Chaos Overlords PX16 image: a Windows bitmap file with width and height
  set to 0 and planes set to 255. The executable supplies width, height and
  planes itself. The pixels are 16-bit RGB555, rows bottom first, each row
  padded to a multiple of 4 bytes.
doc-ref: FMT-GFX-001, FND-GFX-001, FND-GFX-003, FND-PLATFORM-002
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
    doc: Offset of the pixels, always 54.
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
    doc: Bits per pixel, always 16.
  - id: compression
    type: u4
    doc: 0, uncompressed.
  - id: image_size
    type: u4
    doc: Size of the pixel data in bytes, the file size minus 54.
  - id: x_pixels_per_meter
    type: s4
    doc: 0.
  - id: y_pixels_per_meter
    type: s4
    doc: 0.
  - id: colors_used
    type: u4
    doc: 0.
  - id: colors_important
    type: u4
    doc: 0.
  - id: pixels
    size: image_size
    doc: |
      height rows, bottom row first. A row holds width UINT16LE RGB555 pixels
      (bits 0-4 blue, 5-9 green, 10-14 red, bit 15 always 0), then zero bytes
      up to a multiple of 4 bytes. Width and height come from the format
      entry's Coverage table.
    doc-ref: FND-GFX-001, FND-GFX-003

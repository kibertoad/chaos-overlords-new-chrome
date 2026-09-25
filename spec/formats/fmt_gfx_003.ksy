meta:
  id: fmt_gfx_003
  title: Palette entry in a PX08 image file
  license: MIT
  endian: le
doc: One of the 256 colour table entries of a Chaos Overlords PX08 image.
doc-ref: FMT-GFX-003, FND-GFX-002
seq:
  - id: blue
    type: u1
    doc: Blue level.
  - id: green
    type: u1
    doc: Green level.
  - id: red
    type: u1
    doc: Red level.
  - id: reserved_03
    type: u1
    doc: 0 in every entry of every file.

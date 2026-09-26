meta:
  id: fmt_save_002
  title: Short M10W save file
  license: MIT
  endian: le
doc: |
  The 16-byte save form the load function accepts with the marker M10W: the
  marker and one 12-byte block whose meaning is unknown.
doc-ref: FMT-SAVE-002, FND-SAVE-001
seq:
  - id: magic
    type: u4
    enum: magic
  - id: payload
    size: 12
enums:
  magic:
    0x5730314d:
      id: save_magic_m10w
      doc: M10W.

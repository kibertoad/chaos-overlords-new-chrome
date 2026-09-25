meta:
  id: fmt_data_004
  title: Colour list in DATA/CLT00002
  license: MIT
  endian: le
doc: |
  DATA/CLT00002 of Chaos Overlords: 236 four-byte colour entries, back to
  back. Entry n becomes entry n + 10 of the 256-colour palette the game
  builds when the display runs at 8 bits per pixel.
doc-ref: FMT-DATA-004, FND-DATA-004, FND-PLATFORM-011
seq:
  - id: entries
    type: entry
    repeat: eos
types:
  entry:
    doc: One colour, 4 bytes.
    seq:
      - id: red
        type: u1
        doc: Red level, a multiple of 17.
        doc-ref: FND-PLATFORM-011
      - id: green
        type: u1
        doc: Green level, a multiple of 17.
        doc-ref: FND-PLATFORM-011
      - id: blue
        type: u1
        doc: Blue level, a multiple of 17.
        doc-ref: FND-PLATFORM-011
      - id: flags
        type: u1
        doc: 4 in every entry. The loader does not read it and sets the flag itself.
        doc-ref: FND-DATA-004, FND-PLATFORM-011

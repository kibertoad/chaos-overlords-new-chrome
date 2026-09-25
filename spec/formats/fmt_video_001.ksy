meta:
  id: fmt_video_001
  title: Smacker movies DATA/MVINTRO and DATA/MVLOGOS
  license: MIT
  endian: le
  bit-endian: le
doc: |
  A Smacker version 2 movie. The header, the frame size and type tables and
  the block boundaries are described; the Huffman trees and the frames are
  kept as opaque bytes.
doc-ref: FMT-VIDEO-001, FND-VIDEO-001
seq:
  - id: signature
    contents: SMK2
  - id: width
    type: u4
  - id: height
    type: u4
  - id: frame_count
    type: u4
  - id: frame_rate
    type: s4
    doc: Negative values are the frame time in hundredths of a millisecond.
  - id: flags
    type: u4
    doc: 0 in both shipped files.
  - id: audio_buffer_sizes
    type: u4
    repeat: expr
    repeat-expr: 7
  - id: trees_size
    type: u4
  - id: mono_block_tree_size
    type: u4
  - id: colour_tree_size
    type: u4
  - id: full_block_tree_size
    type: u4
  - id: type_tree_size
    type: u4
  - id: audio_rates
    type: audio_rate
    repeat: expr
    repeat-expr: 7
  - id: reserved_64
    type: u4
  - id: frame_sizes
    type: u4
    repeat: expr
    repeat-expr: frame_count
    doc: Frame size in bytes once the two low bits are cleared.
  - id: frame_types
    type: u1
    repeat: expr
    repeat-expr: frame_count
  - id: trees
    size: trees_size
  - id: frames
    size: frame_sizes[_index] & 0xFFFFFFFC
    repeat: expr
    repeat-expr: frame_count
types:
  audio_rate:
    seq:
      - id: sample_rate
        type: b24
      - id: reserved_bits
        type: b4
      - id: stereo
        type: b1
      - id: sixteen_bit
        type: b1
      - id: present
        type: b1
      - id: compressed
        type: b1

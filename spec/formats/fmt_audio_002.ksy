meta:
  id: fmt_audio_002
  title: Ogg page of the music tracks MUSIC/TrackNN.ogg
  license: MIT
  endian: le
  bit-endian: le
doc: |
  One Ogg page as RFC 3533 defines it. A music file is a sequence of these
  pages from its first byte to its last, carrying one Vorbis stream. The
  packets in the body are not described.
doc-ref: FMT-AUDIO-002, FND-AUDIO-005
seq:
  - id: pages
    type: page
    repeat: eos
types:
  page:
    seq:
      - id: capture_pattern
        contents: OggS
      - id: version
        type: u1
        doc: 0.
      - id: continued
        type: b1
        doc: Set when the page continues a packet begun on the previous page.
      - id: first_page
        type: b1
        doc: Set on the first page of the stream.
      - id: last_page
        type: b1
        doc: Set on the last page of the stream.
      - id: unused_bits
        type: b5
        doc: 0.
      - id: granule_position
        type: s8
        doc: Sample position at the end of the last packet completed on the page.
      - id: serial
        type: u4
        doc: Stream serial number, the same on every page of a file.
      - id: sequence
        type: u4
        doc: Page number within the stream, from 0.
      - id: checksum
        type: u4
        doc: CRC-32 of the page as RFC 3533 defines it.
      - id: segment_count
        type: u1
      - id: segment_table
        type: u1
        repeat: expr
        repeat-expr: segment_count
        doc: Length of each segment of the body.
      - id: body
        size: segment_table[_index]
        repeat: expr
        repeat-expr: segment_count
        doc: Packet data, one block per segment_table entry.

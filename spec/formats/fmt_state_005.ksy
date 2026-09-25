meta:
  id: fmt_state_005
  title: Comlink message record
  endian: le
  license: MIT
doc: |
  One 166-byte Comlink message record. The game keeps 16 per player at
  0x0049CA90 + player * 0xA60 + index * 0xA6.
doc-ref: FMT-STATE-005, FND-COMLINK-001, FND-COMLINK-004
seq:
  - id: occupied
    type: u1
    doc: Nonzero when the record holds a message.
    doc-ref: FND-COMLINK-004
  - id: read
    type: u1
    doc: Set when the recipient has viewed the message.
    doc-ref: FND-COMLINK-004
  - id: turn
    type: s2
    doc: Zero-based turn the message was sent in.
    doc-ref: FND-COMLINK-004, FND-COMLINK-008
  - id: sender
    type: u1
    doc: Sending player slot.
    doc-ref: FND-COMLINK-004
  - id: text
    size: 160
    type: str
    encoding: ASCII
    doc: Four rows of 40 characters from 0x20 to 0x5A, padded with spaces, no terminator.
    doc-ref: FND-COMLINK-004, FND-COMLINK-008
  - id: unk_a5
    type: u1
    doc: Spare byte, never written on its own; 0 from the Send buffer.
    doc-ref: FND-COMLINK-004, FND-COMLINK-008

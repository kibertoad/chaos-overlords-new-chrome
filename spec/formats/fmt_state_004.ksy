meta:
  id: fmt_state_004
  title: Site slot in a sector record
  endian: le
  license: MIT
doc: |
  One 2-byte site slot. Each sector record holds three, at 0x07, 0x09 and 0x0B.
doc-ref: FMT-STATE-004, FND-TURN-001
seq:
  - id: definition
    type: s1
    doc: Record number in the site definition table; 21 in slot 0 of a headquarters sector.
    doc-ref: FND-CITY-003, FND-TURN-001
  - id: progress
    type: s1
    doc: Influence progress; complete when equal to the definition's Resistance.
    doc-ref: FND-CONTROL-001, FND-GANG-001, FND-TURN-001, FND-TURN-003, FND-TURN-004

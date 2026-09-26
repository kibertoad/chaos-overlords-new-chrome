meta:
  id: fmt_state_006
  title: Last Turn report record
  endian: le
  license: MIT
doc: |
  One 10-byte Last Turn report record. The game keeps 32 per player at
  0x004AAE08 + player * 0x140 + index * 10.
doc-ref: FMT-STATE-006, FND-EVENT-001, FND-EVENT-004
seq:
  - id: occupied
    type: u1
    doc: 1 when the record holds a report of the last resolution.
    doc-ref: FND-EVENT-004
  - id: unk_01
    type: u1
    doc: Padding; never written by the recorder or the clearing.
    doc-ref: FND-EVENT-004
  - id: report_type
    type: s2
    enum: report_type
    doc: The kind of report.
    doc-ref: FND-EVENT-001, FND-EVENT-004
  - id: arg1
    type: s2
    doc: First argument; its meaning depends on report_type.
    doc-ref: FND-EVENT-004, FND-EVENT-005
  - id: arg2
    type: s2
    doc: Second argument.
    doc-ref: FND-EVENT-004, FND-EVENT-005
  - id: arg3
    type: s2
    doc: Third argument.
    doc-ref: FND-EVENT-004, FND-EVENT-005
enums:
  report_type:
    0: none
    1: crackdown
    2: control_gained
    3: control_lost
    4: site_completed
    5: research_completed
    6: cash_short
    7: hire_sector_full
    8: hire_roster_full
    9: elimination

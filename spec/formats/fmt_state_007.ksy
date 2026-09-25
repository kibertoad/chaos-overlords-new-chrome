meta:
  id: fmt_state_007
  title: Computer player planning record, one per player and roster slot
  endian: le
  license: MIT
doc: |
  One 16-byte planning record of the computer players. The game keeps 486 at
  0x0048A250, player * 0x510 + roster slot * 0x10.
doc-ref: FMT-STATE-007, FND-AI-019, FND-STATE-006
seq:
  - id: family
    type: s1
    enum: family
    doc: Planning family; 99 after a reset.
    doc-ref: FND-AI-019, FND-STATE-006
  - id: unk_01
    type: u1
    doc: Flag read only by selector 0x48.
    doc-ref: FND-STATE-006
  - id: older_action
    type: u1
    doc: Action planned two turns ago.
    doc-ref: FND-AI-019
  - id: older_target
    type: s1
    doc: Its first target byte.
    doc-ref: FND-AI-019
  - id: older_target_2
    type: s1
    doc: Its second target byte.
    doc-ref: FND-AI-019
  - id: previous_action
    type: u1
    doc: Action planned last turn.
    doc-ref: FND-AI-019
  - id: previous_target
    type: s1
    doc: Its first target byte.
    doc-ref: FND-AI-019
  - id: previous_target_2
    type: s1
    doc: Its second target byte.
    doc-ref: FND-AI-019
  - id: planned_action
    type: u1
    doc: Action planned this turn.
    doc-ref: FND-AI-019
  - id: planned_target
    type: s1
    doc: Its first target byte.
    doc-ref: FND-AI-019
  - id: planned_target_2
    type: s1
    doc: Its second target byte.
    doc-ref: FND-AI-019
  - id: unk_0b
    type: u1
    doc: Padding; never addressed.
    doc-ref: FND-STATE-006
  - id: weapon_cooldown
    type: s2
    doc: Turns before the weapon may be replaced.
    doc-ref: FND-AI-019, FND-AI-021, FND-STATE-006
  - id: armor_cooldown
    type: s2
    doc: Turns before the armor may be replaced.
    doc-ref: FND-AI-019, FND-AI-021, FND-STATE-006
enums:
  family:
    99: family_none

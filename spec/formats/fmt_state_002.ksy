meta:
  id: fmt_state_002
  title: Sector record, one per city sector
  endian: le
  license: MIT
  imports:
    - fmt_state_004
doc: |
  One 36-byte sector record. The game keeps 64 of them at 0x004A08E8,
  sector * 0x24.
doc-ref: FMT-STATE-002, FND-CONTROL-001, FND-UI-035, FND-PLATFORM-003
seq:
  - id: owner
    type: s1
    enum: sector_owner
    doc: Controlling player slot, or -1.
    doc-ref: FND-CONTROL-001, FND-EQUIP-001, FND-SETUP-003, FND-UPKEEP-001
  - id: unk_01
    type: s1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_02
    type: s1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: cash_yield
    type: s1
    doc: Cash the owner collects at Upkeep, 1 plus the Cash of completed sites.
    doc-ref: FND-UI-035, FND-UPKEEP-001
  - id: income
    type: s1
    doc: Sector Income, 3 to 7 (disputed).
    doc-ref: FND-CHAOS-001, FND-UI-035, FND-UPKEEP-001
  - id: tolerance
    type: s1
    doc: Sector Tolerance.
    doc-ref: FND-AI-004, FND-UI-035
  - id: support
    type: s1
    doc: Support of the completed sites.
    doc-ref: FND-GANG-001, FND-UI-035
  - id: sites
    type: fmt_state_004
    repeat: expr
    repeat-expr: 3
    doc: The three site slots.
    doc-ref: FND-TURN-001, FND-TURN-003
  - id: unk_0d
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: factory
    type: u1
    doc: Nonzero when the sector has a completed Factory.
    doc-ref: FND-EQUIP-001
  - id: crackdown_turns
    type: s1
    enum: crackdown_turns
    doc: Police Combat phases left, 0 for none, 100 for permanent.
    doc-ref: FND-SETUP-003
  - id: unk_10
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_11
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_12
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_13
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_14
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: unk_15
    type: u1
    doc: Purpose unknown.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_combat
    type: s1
    doc: Sum of the completed sites' Combat modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_defense
    type: s1
    doc: Sum of the completed sites' Defense modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_stealth
    type: s1
    doc: Sum of the completed sites' Stealth modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_detect
    type: s1
    doc: Sum of the completed sites' Detect modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_chaos
    type: s1
    doc: Sum of the completed sites' Chaos modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_control
    type: s1
    doc: Sum of the completed sites' Control modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_heal
    type: s1
    doc: Sum of the completed sites' Heal modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_influence
    type: s1
    doc: Sum of the completed sites' Influence modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_research
    type: s1
    doc: Sum of the completed sites' Research modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_strength
    type: s1
    doc: Sum of the completed sites' Strength modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_blade
    type: s1
    doc: Sum of the completed sites' Blade modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_ranged
    type: s1
    doc: Sum of the completed sites' Ranged modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_fighting
    type: s1
    doc: Sum of the completed sites' Fighting modifiers.
    doc-ref: SRC-RECHAOS-3561D41
  - id: site_martial_arts
    type: s1
    doc: Sum of the completed sites' Martial Arts modifiers.
    doc-ref: SRC-RECHAOS-3561D41
enums:
  sector_owner:
    -1: sector_neutral
  crackdown_turns:
    100: crackdown_permanent

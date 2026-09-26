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
doc-ref: FMT-STATE-002, FND-CONTROL-001, FND-UI-035, FND-PLATFORM-003, FND-STATE-001
seq:
  - id: owner
    type: s1
    enum: sector_owner
    doc: Controlling player slot, or -1.
    doc-ref: FND-CONTROL-001, FND-EQUIP-001, FND-SETUP-003, FND-UPKEEP-001
  - id: base_income
    type: s1
    doc: Income from city generation, 3 to 7.
    doc-ref: FND-STATE-001
  - id: base_tolerance
    type: s1
    doc: Tolerance before the completed sites, 17 minus Income at generation, kept within 1 to 40.
    doc-ref: FND-STATE-001
  - id: cash_yield
    type: s1
    doc: Cash the owner collects at Upkeep, 1 plus the Cash of completed sites.
    doc-ref: FND-UI-035, FND-UPKEEP-001
  - id: income
    type: s1
    doc: Sector Income, a copy of base_income made before each planning phase.
    doc-ref: FND-CHAOS-001, FND-STATE-001, FND-UI-035, FND-UPKEEP-001
  - id: tolerance
    type: s1
    doc: Sector Tolerance, base_tolerance plus the completed sites' Tolerance.
    doc-ref: FND-AI-004, FND-STATE-001, FND-UI-035
  - id: support
    type: s1
    doc: Support of the completed sites.
    doc-ref: FND-GANG-001, FND-STATE-001, FND-UI-035
  - id: sites
    type: fmt_state_004
    repeat: expr
    repeat-expr: 3
    doc: The three site slots.
    doc-ref: FND-TURN-001, FND-TURN-003
  - id: research_level
    type: u1
    doc: 0, or 1 or 2 from completed sites whose special value is 1 or 2; limits the Tech Level of items bought there.
    doc-ref: FND-STATE-001
  - id: factory
    type: u1
    doc: 1 when a completed site's special value is 3, else 0.
    doc-ref: FND-EQUIP-001, FND-STATE-001
  - id: crackdown_turns
    type: s1
    enum: crackdown_turns
    doc: Police Combat phases left, 0 for none, 100 for permanent.
    doc-ref: FND-SETUP-003
  - id: gangs_seen
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: One byte per player slot, 1 when that player has a gang in the sector that the viewing player can see; rebuilt by the city map drawer.
    doc-ref: FND-STATE-001
  - id: site_combat
    type: s1
    doc: Sum of the completed sites' Combat modifiers.
    doc-ref: FND-STATE-001
  - id: site_defense
    type: s1
    doc: Sum of the completed sites' Defense modifiers.
    doc-ref: FND-STATE-001
  - id: site_stealth
    type: s1
    doc: Sum of the completed sites' Stealth modifiers.
    doc-ref: FND-STATE-001
  - id: site_detect
    type: s1
    doc: Sum of the completed sites' Detect modifiers.
    doc-ref: FND-STATE-001
  - id: site_chaos
    type: s1
    doc: Sum of the completed sites' Chaos modifiers.
    doc-ref: FND-STATE-001
  - id: site_control
    type: s1
    doc: Sum of the completed sites' Control modifiers.
    doc-ref: FND-STATE-001
  - id: site_heal
    type: s1
    doc: Sum of the completed sites' Heal modifiers.
    doc-ref: FND-STATE-001
  - id: site_influence
    type: s1
    doc: Sum of the completed sites' Influence modifiers.
    doc-ref: FND-STATE-001
  - id: site_research
    type: s1
    doc: Sum of the completed sites' Research modifiers.
    doc-ref: FND-STATE-001
  - id: site_strength
    type: s1
    doc: Sum of the completed sites' Strength modifiers.
    doc-ref: FND-STATE-001
  - id: site_blade
    type: s1
    doc: Sum of the completed sites' Blade modifiers.
    doc-ref: FND-STATE-001
  - id: site_ranged
    type: s1
    doc: Sum of the completed sites' Ranged modifiers.
    doc-ref: FND-STATE-001
  - id: site_fighting
    type: s1
    doc: Sum of the completed sites' Fighting modifiers.
    doc-ref: FND-STATE-001
  - id: site_martial_arts
    type: s1
    doc: Sum of the completed sites' Martial Arts modifiers.
    doc-ref: FND-STATE-001
enums:
  sector_owner:
    -1: sector_neutral
  crackdown_turns:
    100: crackdown_permanent

meta:
  id: fmt_save_001
  title: Full save file
  license: MIT
  endian: le
  imports:
    - fmt_state_001
    - fmt_state_002
    - fmt_state_003
doc: |
  A full Chaos Overlords save: the marker, 44 blocks copied from the
  game's globals in a fixed order, six participant mappings in the network
  form, and the marker again.
doc-ref: FMT-SAVE-001, FND-SAVE-001, SRC-RECHAOS-3561D41
seq:
  - id: magic
    type: u4
    enum: magic
  - id: gangs
    type: fmt_state_001
    repeat: expr
    repeat-expr: 486
    doc: Block 1, copied from 0x00498DA8.
  - id: sectors
    type: fmt_state_002
    repeat: expr
    repeat-expr: 64
    doc: Block 2, copied from 0x004A08E8.
  - id: cursor_sectors
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 3, copied from 0x004ABBF0.
  - id: portraits
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 4, copied from 0x004A5F00.
  - id: controller
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 5, copied from 0x004AB638.
  - id: elapsed_turns
    type: s4
    doc: Block 6, copied from 0x0049CA68.
  - id: scenario
    type: s4
    doc: Block 7, copied from 0x004ABBE8.
  - id: turn_limit
    type: s4
    doc: Block 8, copied from 0x004A5EF8.
  - id: cash
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 9, copied from 0x004A25E8.
  - id: hire_offers
    type: s1
    repeat: expr
    repeat-expr: 18
    doc: Block 10, copied from 0x004ABBC0.
  - id: hire_orders
    type: s1
    repeat: expr
    repeat-expr: 18
    doc: Block 11, copied from 0x004A27C8.
  - id: research_remaining
    type: u1
    repeat: expr
    repeat-expr: 384
    doc: Block 12, copied from 0x004A2608.
  - id: player_active
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 13, copied from 0x004ABBE0.
  - id: ai_plans
    size: 7776
    doc: Block 14, copied from 0x0048A250.
  - id: ai_first_plan_flags
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 15, copied from 0x00482108.
  - id: ai_hire_limits
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 16, copied from 0x00482110. Per player, the AI hire limit.
  - id: ai_unused_gang_values
    type: s4
    repeat: expr
    repeat-expr: 486
    doc: Block 17, copied from 0x0048DB48. Per gang, a value only AI selector 0x5D reads; always 0.
  - id: ai_takeover_flags
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 18, copied from 0x00482158. Per player, the takeover flag.
  - id: ai_hire_anchors
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 19, copied from 0x0048E2F8.
  - id: ai_hire_roles
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 20, copied from 0x00482128.
  - id: ai_unused_hire_flags
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 21, copied from 0x00482140. Per player, a flag written with the hire role and never read.
  - id: scenario_score
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 22, copied from 0x004A2790.
  - id: crackdown_history
    type: s2
    repeat: expr
    repeat-expr: 128
    doc: Block 23, copied from 0x004ABCC0.
  - id: reports
    size: 1920
    doc: Block 24, copied from 0x004AAE08.
  - id: combat_results
    size: 9600
    doc: Block 25, copied from 0x004A8888.
  - id: combat_records
    type: fmt_state_003
    repeat: expr
    repeat-expr: 486
    doc: Block 26, copied from 0x004A11E8.
  - id: city_map_image
    type: s2
    doc: Block 27, copied from 0x00494830. The city map is image PX10000 plus this value.
  - id: player_names
    size: 72
    doc: Block 28, copied from 0x004A2588.
  - id: overthrow_count
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 29, copied from 0x004A27A8.
  - id: damage_inflicted
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 30, copied from 0x004A5ED8.
  - id: hide_count
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 31, copied from 0x004A25D0.
  - id: cash_spent
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 32, copied from 0x0049CA78.
  - id: casualties
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 33, copied from 0x004AB620.
  - id: cash_earned
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 34, copied from 0x004A27E0.
  - id: attitudes
    type: s4
    repeat: expr
    repeat-expr: 36
    doc: Block 35, copied from 0x004AB590.
  - id: reactions
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 36, copied from 0x004AB650. Per player, the reaction value.
  - id: homicidal_maniac_flags
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 37, copied from 0x004A2600. Per player, 1 when the match was set up under Homicidal Maniac.
  - id: difficulty_bands
    type: s4
    repeat: expr
    repeat-expr: 6
    doc: Block 38, copied from 0x004A2570.
  - id: players_human
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 39, copied from 0x004ABC58.
  - id: preference_1
    type: u1
    doc: Block 40.
  - id: preference_2
    type: u1
    doc: Block 41.
  - id: preference_3
    type: u1
    doc: Block 42.
  - id: modifier_visibility
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 43, copied from 0x004AB588.
  - id: modifier_force_hire
    type: u1
    repeat: expr
    repeat-expr: 6
    doc: Block 44, copied from 0x004A5EF0.
  - id: participants
    type: s4
    repeat: expr
    repeat-expr: 6
    if: magic == magic::save_magic_n40w
    doc: Six participant mappings from 0x00498968.
  - id: closing_magic
    type: u4
    enum: magic
    doc: A second copy of magic.
enums:
  magic:
    0x57303453:
      id: save_magic_s40w
      doc: S40W, the full save.
    0x5730344e:
      id: save_magic_n40w
      doc: N40W, the full save with participants.

meta:
  id: fmt_state_009
  title: Input event record
  endian: le
  license: MIT
doc: |
  The 16-byte input event the window procedure writes at 0x00498360 and the
  event step hands to the screen loops.
doc-ref: FMT-STATE-009, FND-UI-020
seq:
  - id: type
    type: s4
    enum: event_type
    doc: The kind of event, 0 when none arrived.
    doc-ref: FND-UI-020
  - id: a
    type: s4
    doc: Command group, key character, or the window's surface slot.
    doc-ref: FND-UI-020
  - id: b
    type: s4
    doc: Command item, virtual key, or the client x of a button event.
    doc-ref: FND-UI-020
  - id: c
    type: s4
    doc: The client y of a button event.
    doc-ref: FND-UI-020
enums:
  event_type:
    0: event_none
    1: event_command
    2: event_key
    3: event_left_down
    4: event_left_up
    5: event_left_double
    6: event_close
    7: event_paint
    8: event_deactivate
    9: event_activate
    16: event_destroy
    17: event_right_down
    18: event_right_up
    19: event_right_double

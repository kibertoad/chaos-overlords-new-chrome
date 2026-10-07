# DEV-AI-006

- Departs from: RULE-AI-006
- Reason: When the sector selector's common block tests a sector of column 7 with a row of 1 or
  more and the sector passes, the original multiplies a dword past its score table by five: one
  of the first seven dwords of player 0's planning records, which hold the family, action, target
  and cooldown bytes of records 0 and 1 (FND-AI-069). The rebuild leaves the records unchanged.
- Setting: None
- Default: mandatory
- Justification: The write is an index past the end of a 64-entry table, and it rewrites another
  player's planning state with bytes that are no family, action or target any handler assigns: a
  corruption no player can rely on. While a human holds slot 0 the records are zero bytes and the
  multiply leaves them zero, so the usual match plays the same. A setting would choose between
  leaving the records alone and reproducing a corruption the rebuild's planning state refuses to
  hold.
- Dropped: no

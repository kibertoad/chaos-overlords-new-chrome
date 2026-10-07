# DEV-SETUP-001

- Departs from: RULE-SETUP-001, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007
- Reason: In an online match a modifier name typed by one player would change the match for
  every seat. The online lobby refuses those names, and the client replaces a name whose
  ten-character form is a modifier with the seat's derived name. Hot-seat and single-player
  matches keep the original behaviour.
- Setting: None
- Default: mandatory
- Justification: The original's network play is not reproduced (DEV-NET-001), so there is no
  original online behaviour to keep, and hot-seat and single-player matches are unchanged.
- Dropped: no

The reasoning is in `docs/MULTIPLAYER.md`.

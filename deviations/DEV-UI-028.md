# DEV-UI-028

- Departs from: SCR-HIRE-002, SCR-UI-003, SCR-UI-004
- Reason: The keys 1, 2 and 3 hire the offer in that slot of the Hire dock, counted from the
  left, into the selected sector, on the city screen and on the sector view. A key takes the
  offer as a press on its portrait does and places it as a drop on the selected sector does (on
  the sector view, a drop on the workspace): the same refusals, the same queued hire and the same
  flash of the cell. The original hires only by dragging an offer's portrait onto a sector, and
  its dock takes no key.
- Setting: None
- Default: mandatory
- Justification: The key queues the hire a drag queues, and the drag works as before, so it adds
  a way to give an order the player can already give and changes no order or result. Without it
  a player who cannot drag cannot hire at all. A setting that took the keys away would give the
  player nothing, as with the city keys of DEV-UI-020.
- Tests: tests/Rechaos.Tests/KeyboardHireTests.cs
- Dropped: no

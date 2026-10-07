# DEV-UI-023

- Departs from: SCR-UI-003, SCR-UI-004
- Reason: A line at `(438,354)`, between the console and the Hire dock, says in words why an
  order, a drop or a key was refused. A line along the bottom of the city map points to Options >
  Keys, which lists the rebuild's keys, and in an online match says where the turn stands instead.
- Setting: None
- Default: mandatory
- Justification: Both lines add information and change nothing the player can do. The original
  answers a refused order with the reject sound alone, which the rebuild still plays; the line
  says which rule refused it. The key line leads to the keys DEV-UI-020 adds, which the
  original's screens cannot show, and online it gives way to the turn's state, which a player
  waiting on others needs. A setting that hid them would only take information away.
- Dropped: no

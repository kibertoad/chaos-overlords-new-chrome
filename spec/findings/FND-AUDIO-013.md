---
id: FND-AUDIO-013
title: Detailed Combat loads each attack's sound into slot 5 and picks the attack and hit strips from the weapon, Martial Arts and outcome
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042EE46
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F98B
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Detailed Combat, `fn_0042E040`, loads each combatant's attack sound into slot
  5, which the title initialization leaves empty (FND-AUDIO-002):
  - an attack with a weapon equipped loads sound resource `500 + sound`, where
    `sound` is the weapon's sound number from its `ITEMS` record;
  - an attack without a weapon loads `SND00500`, or `SND00501` when the
    attacking gang's base Martial Arts is above 0;
  - a police attack on a gang the police detected loads `SND00518`. A police
    pass that detects no gang is not presented;
  - the value that marks an evaded attack does not load a usable sound.
- The timeline `fn_00430C23` plays slot 5 through the wrapper `fn_00464290`
  just before it starts advancing frames, and the presenter unloads slot 5 after
  that combatant's sequence.
- The last 16-bit word of each 166-byte `ITEMS` record is read only in the two
  side compositors `fn_0042EE46` and `fn_0042F98B`, six reads in all. For each
  equipped weapon, armor and miscellaneous item they load that item's `PX04xxx`
  strip of 48-by-48 frames and copy the frame at that index opaquely into the
  item's aperture. The apertures are at backing-buffer `(100,192)`, `(100,241)`
  and `(100,290)` on the left and `(289,192)`, `(289,241)` and `(289,290)` on
  the right; the panel buffer starts at y 144, so they sit at panel-local y 48,
  97 and 146.
- `fn_0042E040` picks the animation strips as follows. For an unarmed attack in
  the normal direction, base Martial Arts above 0 selects attacker strip
  `PX07001` with target strip `PX07118`, and otherwise `PX07000` with
  `PX07102`. An attack that the hidden target evades selects `PX07027` with
  `PX07100`; without the second strip the target aperture stays black. For
  armed and unarmed attacks alike, damage of 0 replaces the target strip with
  `PX07101`. The mirrored path applies the same choices to the `PX072xx` and
  `PX073xx` strips. It plays an attack on the viewing player's gang, with the
  attacker on the right, and it never plays retaliation, which has no clip or
  sound of its own (FND-COMBAT-004). A detected police attack selects `PX07228`
  with `PX07320` when it does damage and `PX07301` when it does none.

## Interpretation

Each attack Detailed Combat shows sounds once, from its first frame, with the
sound of the attacker's weapon, of bare hands, of Martial Arts, or of the
police. The last word of an `ITEMS` record is the frame of the item's rotation
strip that combat shows, a presentation value with no effect on the rules. The
unarmed choice tests only whether base Martial Arts is positive; it does not
compare Martial Arts with Strength or Fighting.

## Alternatives

- The "base" Martial Arts is read as the gang definition's value, without
  equipment or sites. The field read has not been named against a format entry.

## How to reproduce

In `0x0042E040`, find the constant 500 added to the item's sound byte before the
call to the sound loader `0x0045867C` with slot 5, and the constants 7000,
7100, 7200 and 7300 that the strip numbers are built from. In `0x0042EE46` and
`0x0042F98B`, find the reads at offset 164 of the item record.

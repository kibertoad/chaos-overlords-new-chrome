---
id: FND-AI-074
title: The family handlers write only the target bytes an action uses, and families 3, 5 and 7 store the focus by action
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041FEF0..0x0042094F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00431C60..0x0043407F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00434080..0x004384BF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043A1D0..0x0043B27F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Every store of a planned action in the family handlers writes the action byte
of the planning record (`0x0048A258 + player * 0x510 + slot * 0x10`) and of
the gang record (`0x00498DAF + player * 0xA20 + slot * 0x20`). The target
stores that follow depend on the action. Attack (1) stores both target bytes
in both records (`+0x09`, `+0x0A` of the planning record, `+0x08`, `+0x09` of
the gang record). Move (10), Equip (5), Influence (9) and Research (11) store
only the first. Every other action the handlers write, None (0), Chaos (3),
Control (4), Heal (7), Hide (8), Snitch (13) and Terminate (14), stores no
target byte. This holds for every action store of the handlers of families 0
to 7 and 9 to 14 (FND-AI-001).

The Greed Terminate branch of the seven handlers that have one, families 2
(`0x00420906`), 6 (`0x00432777`), 1 (`0x00435353`), 12 (`0x00435B7B`), 3
(`0x00436C21`), 7 (`0x00438479`) and 5 (`0x0043B23B`), stores 14 in both
action bytes and 1 in `needs_family` (`0x0048A251`) and nothing else. Family
2's late Control override (`0x0042087E`) stores the action and -1 in the
focus and no target.

The focus value of the auxiliary record (`0x0048C0BA + player * 0x46E +
slot * 14`, FND-AI-081) is stored by families 3 (`0x00435BD0`) and 5
(`0x0043A1D0`) after each action they plan:

| Action | Family 3 stores | Family 5 stores | Value |
|---|---|---|---|
| Heal | `0x00435C93`, `0x0043626F` | `0x0043A293`, `0x0043A84C` | -1 |
| Influence | `0x00435DD1`, `0x00436354`, `0x00436490`, `0x00436917` | `0x0043A3D1`, `0x0043A916`, `0x0043AA52`, `0x0043AF31` | the gang's sector |
| Move | `0x00435EE0`, `0x00435F03`, `0x00436547`, `0x00436A26`, `0x00436ADF` | `0x0043A4E0`, `0x0043AB61`, `0x0043B040`, `0x0043B0F9` | -1 |
| Equip | `0x00436070`, `0x004361C1` | `0x0043A64D`, `0x0043A79E` | -1 |
| Attack | `0x0043675C` | `0x0043AD76` | the gang's sector |
| None | `0x004367B6`, with -1 in `coverage_sector` at `0x004367D9` | `0x0043ADD0`, with -1 in `coverage_sector` at `0x0043ADF3` | -1 |

The sector is the local the handler fills once from selector `0x5A` at its
entry (`0x00435BED`, `0x0043A1ED`), copied for the Attack branch. Control
stores no focus in either handler.

Family 7's two Equip branches (`0x00436F7D..0x0043700A` and
`0x004370AB..0x00437135`) store the action, the item and the cooldown and no
focus. They run only when the cached weight of the gang's sector is not 10
(`0x00436C70` compares it with 10 before the Attack draw and jumps to the
equipment step otherwise); after either branch the handler goes on at
`0x00437138`.

## Interpretation

`plan` writes the target bytes an action uses and leaves the others. Because
each planning pass clears the planned triplet before the handlers run
(FND-AI-019), the bytes left alone are 0 unless an earlier write of the same
pass stored them, as when the Greed Terminate or family 2's Control override
replaces a planned Move or Attack: the record keeps that action's targets.

Families 3 and 5 keep their focus in step with the action: the sector of the
site they influence or the fight they start, -1 after Heal, Move, Equip and a
failed draw, and unchanged after Control. Family 7's Equip keeps the focus of
the pass before, which family 7 compares with its best research sector at its
next pass (FND-AI-035), so an item number left there from a Research can match
a sector number. This corrects FND-AI-035 and FND-AI-015, which read family
7's Equip as storing -1 in the focus.

## Alternatives

The table pairs each focus store with the closest action store before it in
address order; a store reached from two branches would be listed once. The
decompiled control flow of both handlers puts every focus store in the branch
of the action it follows.

## How to reproduce

List the references to `0x0048A258`, `0x0048A259`, `0x0048A25A`, `0x0048A251`
and `0x0048C0BA` inside the handler ranges of FND-AI-001, and pair each target
or focus store with the action store before it.

---
id: FND-AUDIO-011
title: Panels play slot 3 for an accepted choice and slot 4 for a refused one, and the pagers stop at both ends
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418CCC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D61A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451602
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004543EE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E7CE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00453A8D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A8888
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Pairing the direct slot-4 calls of the wrapper `fn_00464290` with the
  resources their functions load through `fn_00464108` puts slot 4 in the
  handlers for Hire (`PX05000`, `PX05016`), Item and Site Information
  (`PX05001`, `PX05002`), Attack, Equip, Influence, Move and Research
  (`PX05003` to `PX05007`), City and Sector Financial (`PX05008`, `PX05019`),
  Last Turn Events, Player Ranking, Combat Results and Detailed Combat
  (`PX05010` to `PX05014`), Give (`PX05015`), the incoming and outgoing Comlink
  panels (`PX05017`, `PX05018`, `PX05023`), Gang Definition Information
  (`PX05022`) and Search: Sites (`PX05024`). In each, slot 4 sits in a branch
  taken when the input is refused, not on the path that opens the panel.
- Accepted confirmations and cancellations go through the keyboard helper
  `fn_00418CCC` or the pointer helper `fn_00418821`. Both play slot 3 before
  they draw the accepted control pressed. Branches that refuse a command or a
  step past a boundary can skip those helpers and call slot 4 directly.
- The Last Turn Events handler `fn_0044F2FC`, the Combat Results handler
  `fn_00451F80` and the incoming Comlink handler `fn_0045D61A` page the same way
  for keys and pointer: Previous on the first page and Next on the last page
  leave the page unchanged and play slot 4. A step that is allowed calls the
  panel's arrow helper (`fn_00451602`, `fn_004543EE` or `fn_0045E7CE`), which
  plays slot 3 before drawing the pressed arrow and changing the page. Paging
  does not wrap from the last page to the first or back.
- Combat Results pages by sector. `fn_00451F80` scans sectors 0 to 63 and keeps
  the qualifying ones in that order. Its table at `0x004A8888` has a stride of
  `0x96` bytes per sector and `0x18` bytes per player: each player row holds six
  four-byte result pairs. The combat resolver `fn_00472775` sets those six
  entries to -1, packs resolved gang records into them, and keeps a police flag
  per player at `0x004A8918`. A sector qualifies when the viewing player has a
  result there or has a gang there now, and at least one player has a result
  there.
- The renderer `fn_00453A8D` walks all six entries of a row and lays them out as
  two grids of two columns by three rows. Its callers at `0x0045351E` and
  `0x00453A78` pass origins `(0x67,0xAD)` and `(0xF6,0xAD)`, and the renderer
  adds 44 by the entry's parity and 52 by its pair, so the grids start at local
  x 103 and 246 and screen y 173. The centre column always shows the other five
  players in player order, draws a dim portrait for a player with no result in
  the sector, starts on the first opponent that has one, and accepts only
  players with a result. Choosing another opponent plays slot 3 through the
  wrapper before the redraw; choosing the selected one or one with no result
  plays nothing.

## Interpretation

`SND00203`, in slot 3, is the sound of an accepted choice, and `SND00204`, in
slot 4, the sound of a refused one. A panel does not play slot 4 when it opens,
only when the player asks it for something it refuses. The three pagers stop at
both ends and sound the refusal there.

## Alternatives

None known.

## How to reproduce

List the callers of the wrapper at `0x00464290` that push the constant 4, and
for each the constant resource number its function passes to `0x00464108`. In
`0x0044F2FC`, `0x00451F80` and `0x0045D61A`, the boundary branches push 4 and
the step branches call the arrow helpers, which push 3. The Combat Results table
address `0x004A8888` and stride `0x96` appear in `0x00451F80`.

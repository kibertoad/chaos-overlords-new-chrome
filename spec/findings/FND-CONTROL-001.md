---
id: FND-CONTROL-001
title: The Control pass pools strength per player, settles sectors in ascending order and keeps a neutral candidate ahead of zero-margin ties
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004756D9
tool: Ghidra 12.1.3
environment: null
---

## Observation

The whole-turn resolver `fn_00472775` has a Control block that runs in two
steps.

1. A scan over player slots 0 to 5 and, inside each, the player's 81 gang
   records adds the strength of every record whose action is 4 (Control) into
   per-player strength cells. All six cells start at 0, and only records with
   action 4 add to them.
2. A loop over the sectors in ascending sector number then settles each
   sector. For each sector it sets its best margin to 0, its winner to -1 and
   the first entry of a candidate array to -1. It scans player slots 0 to 5
   and, for each, subtracts the sector's Income, the strength of the defending
   gangs and the Support of the influenced sites from that player's pooled
   strength. A margin strictly greater than the best replaces the candidate
   list with that player; an equal margin appends the player to the list. The
   scan does not test whether the player gave any Control order.
3. When the list holds more than one candidate, the call at `0x004756D9`
   passes the candidate count to the bounded random wrapper, which returns a
   value from 1 to the count. The code indexes the candidate array through a
   four-byte slot that sits just before it, so the value 1 selects the first
   candidate.
4. The sector's owner is replaced only when the selected candidate is not -1.
   The write goes to the owner byte of the sector record at
   `0x004A08E8 + sector * 0x24`, and the same code clears the sector's
   influence totals.

The observation does not say which byte of the sector record the loop reads
as Income.

## Interpretation

The random draws of Control are made in ascending sector order, whatever
order the players gave their orders in, and each draw is one call of the
bounded wrapper. A player's strength in a sector is pooled from its gangs in
player slot and roster slot order. A negative margin never wins. A single
player with the largest positive margin wins without a draw. Several players
tied at the largest positive margin are chosen between with one draw, in
ascending slot order. At a best margin of 0 the neutral entry -1 stays first
in the list: a draw of 1 leaves the owner unchanged and 2 and above pick the
tied players in slot order. One challenger at margin 0 therefore wins half the
time, and each of `n` tied challengers wins with probability `1 / (n + 1)`.

Because the scan covers all six slots with their zero-strength cells, a player
who gave no Control order has margin `-(Income + defense + Support)`. When that
sum is negative, such a player can tie or beat the real challenger and be
written as the owner.

## Alternatives

- The strength added per gang is taken to be Force plus Control, as the manual
  describes; the observation calls it the action's strength without listing
  the terms.
- Whether the sector loop skips sectors where no player gave a Control order,
  or sectors under a Crackdown, has not been recorded. If it skips neither, the
  zero-strength effect above applies to every sector whose Income, defense and
  Support sum below 0, every turn.
- Whether the owner's own gangs count as defenders when the owner is one of
  the candidates has not been read.

## How to reproduce

Open `fn_00472775`, the whole-turn resolver, and find the pass that tests
action 4 in a player-then-roster double loop. The following loop over 64
sectors holds the candidate array and the call at `0x004756D9` to the bounded
random wrapper; the owner write goes to `0x004A08E8 + sector * 0x24`.

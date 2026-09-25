---
id: FND-HIRE-001
title: Hire offers start at -100, are refilled only at planning entry, and are negated in place when hired or snubbed
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004716EB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F4E3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FE8B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078B8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F706
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472750
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047592B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004759BD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475AC4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475BDB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475C88
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475CBA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBC0..0x004ABBD2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A27C8..0x004A27DA
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Two 18-byte arrays hold three entries per player slot, element
  `player * 3 + slot`: the offers at `0x004ABBC0` and the orders at
  `0x004A27C8`.
- The fresh-game path of `0x0046E766` writes the literal byte `0x9C` (-100)
  into every offer entry and `0xFF` (-1) into every order entry, for all six
  players and all three slots. It makes no random draw there.
- `0x004078B8` writes -2 into the order entry of the selected slot.
- `0x004716EB` scans slots 0, 1 and 2 of one player. For each slot whose offer
  is negative, it draws a gang definition number with the bounded random
  wrapper over 1 to 89, repeating the draw while the result equals the value of
  any slot that is currently positive, or equals the negation of the old value
  in the slot being filled. It writes the accepted number into that same slot.
  Its only callers are the call sites `0x0046F4E3` (computer planning entry)
  and `0x0046FE8B` (human planning entry). The resolver `0x00472775` does not
  call it.
- The outer loop reaches the whole-turn resolver through `0x0046F706`, which
  calls `0x004726C0`, which calls `0x00472775` at `0x00472750`.
- The hire block of `0x00472775` scans player slots 0 to 5 and, for each,
  offer slots 0 to 2. For a snub order (-2) it negates the offer in that slot
  and sets the order to -1. For an order holding a sector it runs these steps
  in this order:
  1. From `0x0047592B` to `0x004759A8`, it counts the player's gangs whose
     sector byte equals the order's sector (see FND-HIRE-002). A count of six
     fails the hire with no random draw.
  2. From `0x004759BD` to `0x00475A15`, it compares the player's current cash
     with the definition's hire cost; the signed `JG` at `0x00475A15` takes the
     failure path (FND-EQUIP-006).
  3. At `0x00475AC4`, it draws Force as the bounded wrapper over 1 to 5, plus 4
     (unless the name modifier of FND-HIRE-005 applies).
  4. From `0x00475BDB` to `0x00475C2D`, it searches the player's first 80 gang
     records for a free one. A full roster fails the hire after the Force draw.
  5. On success it copies a complete new gang record into the free slot, adds
     the hire cost to cash spent from `0x00475C88` to `0x00475CA8`, and
     subtracts it from cash from `0x00475CBA` through the instruction at
     `0x00475CE4`.
- A successful hire negates the offer in its slot and sets the order to -1. A
  failed hire sets the order to -1 and leaves the offer unchanged. Rejected,
  cancelled and failed hires change neither cash nor cash spent.
- The successful path reads only the definition's hire cost, not its Upkeep.
  The Upkeep scan that charges active gangs is at the top of the next
  iteration of the outer loop in `0x0046E766`.

## Interpretation

The first offers appear at each player's first planning entry, not at setup.
A hired or snubbed offer stays negated in its own slot until the player's next
planning entry, when it is refilled in place; slots never shift. Replacement is
rejection sampling over definitions 1 to 89: the new offer differs from the
other positive offers and from the gang just removed from that slot. A hired
gang starts with Force 5 to 9. Choosing a hire reserves no cash; the cost is
checked and paid at resolution, and a failed hire does not refill its slot.

A gang hired in turn N pays its hire cost at the end of turn N and its first
Upkeep at the start of turn N + 1, since the new record has a real sector and
the Upkeep scan counts it.

## Alternatives

An earlier reading counted every player's gangs in the target sector for the
six-gang limit. FND-HIRE-002 shows that the count covers the hiring player
only.

Whether the rejection test excludes definition 0 (the Right Hands) other than
through the 1 to 89 range, and whether it excludes gangs other players are
offered or have hired, has not been read; nothing in the observed test does.

Which 80 of the 81 records the free-slot search covers (0 to 79 or 1 to 80) is
not written down.

## How to reproduce

The arrays at `0x004ABBC0` and `0x004A27C8` are referenced by the fresh-game
loop in `0x0046E766`, by refill helper `0x004716EB` and by the hire block of
`0x00472775`. Find the callers of `0x004716EB` to reach `0x0046F4E3` and
`0x0046FE8B`. In `0x00472775`, the hire block is the loop whose inner bound is
3 and that indexes `0x004A27C8`; the listed instruction addresses fall inside
it.

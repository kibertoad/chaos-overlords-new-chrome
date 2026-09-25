---
id: FND-RNG-006
title: The 61 bounded draws, call by call, all made on the main thread; no pointer to rand or to any function that reaches it is stored
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D227
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00478CD0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475F70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408214
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401EF0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

### The call sites

Ghidra lists 61 calls to the bounded wrapper `fn_0045D227` (FND-RNG-003). Each
passes its bound as the only argument. In the table, "sel" is a selector of the
computer players' query function `fn_00402D70` whose result is the bound, and
the place is the part of the turn the call runs in (glossary terms). Function
ranges are in FND-EXE-004.

Startup and setup (8 calls):

| Call | Function | Bound | Result used for | Place |
|---|---|---|---|---|
| `0x00464726` | `fn_0046439A` | 16384 | high half of a missing serial number, minus 1 | process start, once (FND-RNG-001) |
| `0x00464739` | `fn_0046439A` | 16384 | low half of the same serial number, minus 1 | process start, once |
| `0x00468C99` | `fn_00468C8E` | 15 | portrait of an empty setup slot, minus 1; repeated while the portrait is taken | Begin of local setup (`fn_0040E0A0`, call `0x0040EA87`) or network setup (`fn_004677F0`, call `0x00468562`) |
| `0x0046DC83` | `fn_0046DC10` | 4 | reaction, the drawn value plus 2 (3 to 6), stored at `0x004AB650` + 4 × player | new-game initialization, once per player, none under Homicidal Maniac (FND-RNG-005) |
| `0x00476191` | `fn_00475FE1` | 32 | X of a density centre, minus 1 (40 centres) | city generation |
| `0x0047619F` | `fn_00475FE1` | 32 | Y of the same centre, minus 1 | city generation |
| `0x004764C1` | `fn_004764B6` | 21 | a site proposal, minus 1 (FND-CITY-002); `fn_004764B6` is called at `0x004763AB`, `0x004763BD` and `0x0047640B` | city generation |
| `0x00476777` | `fn_00476726` | 6 | headquarters permutation, minus 1, repeated on a clash (FND-CITY-003) | after city generation |

Planning (1 call outside the computer players):

| Call | Function | Bound | Result used for | Place |
|---|---|---|---|---|
| `0x0047172A` | `fn_004716EB` | 89 | a gang definition for a vacant offer slot of `active_player`; drawn again while it equals one of that player's three offers or the negation of this slot's value | `planning_phase`, at each player's planning entry (calls `0x0046F4E3` for a computer player, `0x0046FE8B` in the human handler) |

Resolution (5 direct calls in `fn_00472775`, and one in the dice helper):

| Call | Function | Bound | Result used for | Place |
|---|---|---|---|---|
| `0x00475FBB` | `fn_00475F70` | 6 | one die: counted as a success when at least the helper's threshold (capped at 6); the helper makes one call per die, none when the count is 0 or less | wherever the resolver rolls dice, below |
| `0x004737A9` | `fn_00472775` | 3 | Crackdown length: the sector's `crackdown_turns` becomes its old value + the draw + 2 | `chaos_phase`, only for the third Crackdown in the window, after the sector is made neutral (FND-POLICE-004) |
| `0x00473ABC` | `fn_00472775` | 20 | an attack on a gang whose `action` is Hide misses when the draw is below the target's `stealth` + 14 - the attacker's `detect` (+ 10 instead of + 14 for `difficulty_band` 2) | `combat_phase` |
| `0x0047419B` | `fn_00472775` | 100 | police find a gang in a sector with positive `crackdown_turns` when the draw is at most 100 - (20 if hiding) - 5 × `stealth` + 15 | `police_phase` |
| `0x004756D9` | `fn_00472775` | ties + 1 | which of the tied players takes the sector | `control_phase`, only when the best Control strength is tied |
| `0x00475AC6` | `fn_00472775` | 5 | Force of a hired gang, the draw + 4 (5 to 9); no draw and Force 10 when `hire_force_modifier` is set | `hire_phase` |

`fn_00475F70` has 19 callers, all in `fn_00472775`. Three calls per action,
one for each `difficulty_band` (0, 1 and 2): Heal at `0x00472C1B`,
`0x00472C3B`, `0x00472C5B`; Influence at `0x00472DC3`, `0x00472DE9`,
`0x00472E0F`; Research at `0x00472F50`, `0x00472F76`, `0x00472F9C`; Chaos at
`0x004732EA`, `0x00473324`, `0x0047335E`; the attack at `0x00473BC1`,
`0x00473BE3`, `0x00473C05`; the retaliation at `0x00473E18`, `0x00473E40`,
`0x00473E68`. The police damage call at `0x004741D6` is the only one with no
band variant. In address order, which is the order in which the resolver runs
them, the resolver's draws are: Heal, Influence and Research dice in
`instant_phase`; Chaos dice, then the Crackdown length of each sector
neutralized, in `chaos_phase`; for
each attacking gang the Hide test, the attack dice and the retaliation dice;
then the police test and police dice for each gang in turn; the Move-capacity
repair's neighbour draw (below) in `move_phase`; the Control tie-break in
`control_phase`; and the Force draw in `hire_phase`.

The computer players (46 calls). Every one runs while a computer player plans,
through the computer planner `fn_00458FA0` (called at `0x0046F4EC`), except
`0x0040868B`:

| Call | Function | Bound | Result used for |
|---|---|---|---|
| `0x0040868B` | `fn_00408642` | 8 | mode 0 of the sector selector: one of the eight neighbouring offsets, drawn again until the neighbour is inside the city |
| `0x00409C24` | `fn_00408642` | count of sectors tied for the best score | pick among the tied sectors, only when more than one ties; adjacent-target branch |
| `0x00409C99` | `fn_00408642` | same | the same pick in the routing branch |
| `0x004083D4` | `fn_00408214` | 2 | mode 0 placement: lowest or highest occupied sector, when one of them has room |
| `0x004084B4` | `fn_00408214` | sel `0x7A`, the player's owned sectors plus occupied places | the placement place, looked up with sel `0x7B` |
| `0x00433DCB` | `fn_00432DA0` | sel `0x28` | the dispatcher's attack-target draw, from human players' visible gangs |
| `0x00433E0C` | `fn_00432DA0` | sel `0xAA` | the same draw from every other player's visible gangs |

The other 39 are target draws in twelve family handlers. Each passes a count
of visible gangs in the gang's sector as the bound: sel `0x28` counts the
visible gangs of players whose controller is 0 or 3 (humans), sel `0xAA` those
of every other player, sel `0xAB` those of players the drawer views
negatively, and sel `0xAD` those of the sector's owner. The drawn ordinal is
then looked up in the matching list.

| Family | Handler | Calls (bound) |
|---|---|---|
| 0 | `fn_00428EF0` | `0x00429182` (`0x28`), `0x004291C3` (`0xAA`), `0x00429579` (`0x28`), `0x004295BA` (`0xAA`), `0x00429ABB` (`0x28`), `0x00429AFC` (`0xAA`), `0x0042A16A` (`0x28`), `0x0042A1AB` (`0xAA`) |
| 1 | `fn_00434080` | `0x00434ABC` (`0x28`), `0x00434AFD` (`0xAA`) |
| 2 | `fn_0041FEF0` | `0x0042041A` (`0x28`), `0x0042047A` (`0xAB`), `0x004204BE` (`0xAA`) |
| 3 | `fn_00435BD0` | `0x004365DA` (`0x28`), `0x0043661B` (`0xAA`) |
| 4 | `fn_00401000` | `0x004012D0` (`0x28`), `0x00401311` (`0xAA`), `0x004019D9` (`0x28`), `0x00401A1A` (`0xAA`) |
| 5 | `fn_0043A1D0` | `0x0043ABF4` (`0x28`), `0x0043AC35` (`0xAA`) |
| 6 | `fn_00431C60` | `0x00431D0E` (`0x28`), `0x00431D4F` (`0xAA`), `0x004322AF` (`0x28`), `0x004322F0` (`0xAA`) |
| 7 | `fn_00436C70` | `0x00436D14` (`0x28`), `0x00436D55` (`0xAA`) |
| 9 | `fn_004605E0` | `0x004609E7` (`0x28`), `0x00460A28` (`0xAA`) |
| 12 | `fn_004353A0` | `0x00435468` (`0x28`), `0x004354A9` (`0xAA`) |
| 13 | `fn_0040ABC0` | `0x0040ACE9` (`0x28`), `0x0040AD2A` (`0xAD`), `0x0040B082` (`0x28`), `0x0040B0C3` (`0xAA`) |
| 14 | `fn_00466910` | `0x00466A39` (`0x28`), `0x00466A7A` (`0xAD`), `0x00466DCD` (`0x28`), `0x00466E0E` (`0xAA`) |

In each handler the calls come in pairs on the two arms of one branch, the
human-only pool (`0x28`) on one arm and a wider pool on the other; family 2's
branch has three arms. Which test picks the arm belongs to each family's
handler finding.

`fn_00408642` is called with mode 0 only from the Move-capacity repair
`fn_00476A94` (call `0x00476E9C`), which the resolver calls for each player at
`0x004751B2` in `move_phase`. The mode 0 block starts at `0x0040867F`: when
the mode argument is 0 it calls the wrapper with 8, maps results 1 to 8 to the
offsets -9, -8, -7, -1, +1, +7, +8, +9, rejects an offset that leaves the city
or wraps past a row edge, and draws again until one is accepted. The nonzero
modes branch away at `0x00408683` before that call.

### Threads

The program entry point `0x00478D00` calls `fn_00460CCF` at `0x00478E4B`;
`fn_00460CCF` has no other caller. Walking the direct callers of
`fn_0045D227` upward reaches 92 functions, and the only one without a caller is
the entry point. None of those 92 functions, nor `fn_0045D227` nor
`fn_00478CD0`, has a data reference: no code or table stores their address,
and the 4-byte values `0x0045D227` and `0x00478CD0` do not occur in the file.
Their only references are direct calls.

`CreateThread` is called twice, both in `fn_00401EF0` (at `0x00401FF6` and
`0x00402049`), with the start routines `0x0040259A` and `0x00402242`. Neither
start routine is among the 92 functions. `timeSetEvent` is called only in
`fn_004327DC`, with the callback `fn_004327C0`, which sets one byte of the
timer flags at `0x00494810` and returns.

The seed call at `0x00465905` is in `fn_00465620`, which `fn_00460CCF` calls
at `0x00460CF7`.

## Interpretation

The 61 calls are every draw the game makes: 2 at startup, 6 in setup, city
generation and headquarters placement, 1 in the offer refill, 6 in the
resolver and its dice helper, and 46 by the computer players. Every draw,
and the seeding, runs on the thread that enters the program at the entry
point; no second thread and no timer callback can reach the generator, so no
draw starts from the runtime's per-thread default state of 1.

FND-RNG-004's statement that none of the computer players' calls is made while
a turn is resolved is wrong for one call: the mode 0 neighbour draw at
`0x0040868B` is made only in `move_phase`, through the Move-capacity repair. The
other 45 run in `planning_phase`.

Mode 0 of the sector selector is the eight-neighbour draw with rejection that
FND-AI-005 describes. The reading in FND-MOVE-001, a draw among 64 tied sectors,
does not apply to mode 0: that path starts only for nonzero modes.

## Alternatives

The walk up the call graph follows direct calls only. A call through a
computed pointer could still reach one of the 92 functions only if their
address were stored somewhere, and none is. A thread started some other way
than `CreateThread` (for example by a DirectX or multimedia library) runs
library code, which does not call into the game.

The purpose of each computer player's pair is taken from the selectors; which
family test chooses between the two arms is in the computer players' findings,
not here.

## How to reproduce

List the calls to `0x0045D227` in Ghidra (61). For each, read the argument
pushed before the call; for the computer players it is the return value of
`0x00402D70`, whose selector is the second push before that call. For the dice
helper, list the calls to `0x00475F70` (19). For the threads, find the import
address table slots of `CreateThread` (`0x004AE6F8`) and `timeSetEvent`
(`0x004AE908`) and list their references; walk the callers of `0x0045D227`
up to the entry point and check each function's references for data
references.

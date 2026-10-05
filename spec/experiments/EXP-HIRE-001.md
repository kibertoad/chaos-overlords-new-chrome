---
id: EXP-HIRE-001
title: How do drags and Reject presses on the Hire dock set the hire orders at the first planning entry?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-HIRE-001.json
---

## Question

No recorded run uses the Hire dock: every hire the fixtures hold was written
into `hire_orders` by the probe. Does a drag onto a sector or a Reject press
set the orders as RULE-HIRE-003 gives?

## Setup

As EXP-UI-001.

## Procedure

Run `Rechaos.OriginalProbe new-game --executable <copy> --scenario 0 --seed
52421 --hire-steps drag:0:12,drag:1:12,drag:1:0,reject:1,reject:1,reject:2,drag:2:12,reject:0,reject:0`.
The probe stops at the first planning entry as EXP-UI-001 does, dumps the
state, and then takes each step, keeping all 18 bytes of `hire_orders`
(FND-HIRE-001) after it:

- `reject:s` posts a left-button press and release at the centre of offer
  slot `s`'s Reject cross, `(488 + 66s, 443)` (SCR-HIRE-002).
- `drag:s:sector` posts a left-button press at the centre of the offer's
  portrait, `(472 + 66s, 405)`, then a release at the centre of the sector's
  city map cell, `(29 + 54 * (sector % 8), 68 + 52 * (sector / 8))`. Between
  the two it writes the pointer to the press point, halfway and to the
  release point, into both pointer points the window procedure keeps
  (`0x0049859C` and `0x004985A0`, FND-UI-020), which the Hire handler reads
  while it waits for the drag to start and while it drags (FND-HIRE-008). It
  posts no `WM_MOUSEMOVE`, which would replace the second point with the
  desktop cursor's.

The probe waits 0.8 seconds after each step.

## Observations

Begin made 310 calls of `roll`, as in EXP-UI-001. The human, player 0, owns
sector 12, where its only gang stands; sector 0 is neither owned by it nor
holds a gang of it. Its three order bytes after each step are:

| Step | Orders |
|---|---|
| drag 0 to 12 | 12, -1, -1 |
| drag 1 to 12 | -1, 12, -1 |
| drag 1 to 0 | -1, 12, -1 |
| reject 1 | -1, -1, -1 |
| reject 1 | -1, -2, -1 |
| reject 2 | -1, -1, -2 |
| drag 2 to 12 | -1, -1, 12 |
| reject 0 | -2, -1, -1 |
| reject 0 | -1, -1, -1 |

The other fifteen bytes stay -1.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` reaches the same state
as the original at the dump. `TheHireDockSetsTheOriginalsOrders` in
`OriginalNewGameExperimentTests.Presentation.cs` takes each drop through the
rebuild's dock placement check and `QueueHire` and each Reject through
`SnubHireOffer`, and its pending hire and snubbed offer give the same three
orders after every step.

## Conclusion

The run agrees with RULE-HIRE-003: a drop on an owned sector orders the hire
and clears the other slots, a drop elsewhere changes nothing, Reject cancels a
hire, then snubs, then clears the snub, a Reject clears the other slots, and a
snubbed offer dropped on a sector is ordered hired.

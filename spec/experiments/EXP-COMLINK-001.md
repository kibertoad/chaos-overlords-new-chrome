---
id: EXP-COMLINK-001
title: Do the Comlink panels of a local game with three humans store, cap, show, mark and drop messages as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs, Detailed Combat and Slide Panels switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMLINK-001.json
---

## Question

Every Comlink entry rests on static readings alone. With several humans at one
computer, does the original store a copy of a sent message for each selected
recipient and keep only the newest 16, refuse the cards and the sends the spec
says it refuses, overwrite the four-by-40 grid as the keys arrive, open View at
the oldest unread message, mark a shown message read and date it from its
turn, and drop the read messages at the front of an inbox when its player
finishes planning?

## Setup

As EXP-SETUP-001, with humans in slots 0, 1 and 2 and computer players in
slots 3 to 5 (`--humans 0,1,2`), the registry's scenario and Mentality, and
`--seed 1`. At the instruction after the preference loader's call the probe
wrote 0 to Warn if Idle Gangs, Detailed Combat and Slide Panels
(FND-OPTIONS-001), so no prompt waits for input and each panel opens and
closes without sliding. Comlink makes no draw, so the seed does not matter.

## Procedure

The probe ran the script that the fixture lists as `comlink` inputs, one step
each (`Rechaos.OriginalProbe new-game --humans 0,1,2 --seed 1 --comlink
<script>`, then `extract-comlink`). It breaks on the handoff card
`0x004396C0` (FND-SETUP-016), the View handler `0x0045D61A`, the Send handler
`0x0045EAB1`, the View helper `0x0045E04D`, the drop function `0x00460391`
(FND-COMLINK-002 to FND-COMLINK-007) and the sound wrapper `0x00464290`
(FND-AUDIO-011). A step:

- `visit p` waits for player p's handoff card, presses Ready at (320, 265),
  closes the Combat Results panel, and checks `active_player`.
- `view` and `send` press the upper and lower parts of the console's Comlink
  control at (576, 142) and (576, 166) (FND-UI-032); `next`, `prev` and
  `dismiss` press View's controls; `card p`, `press send` and `press cancel`
  press Send's (FND-COMLINK-003, FND-COMLINK-007).
- `type` posts a `WM_KEYDOWN` for each character with its United States
  virtual key, and for Backspace, Enter, the four arrows and Execute
  (`0x2B`), which the window procedure turns into an input event with the
  character `MapVirtualKeyA` gives (FND-UI-020).
- `done` presses Done.

After each step the probe kept the panels open, the effect slots played, each
call of the View helper with the player's cursor and the four numbers it drew
with `0x00414187`, each call of the drop function with the count before and
after it, the six selection bytes at `0x00498114` and the draft at
`0x00498120`, and for most steps every player's `comlink_count`,
`comlink_cursor`, `comlink_pending` and 16 message records.

In turn 1 player 0 presses View, opens Send, presses the card of computer
player 3, its own card and Send with nothing selected, selects player 2,
deselects it and selects players 1 and 2, types the keys of the fixture's
`type` steps and sends with Execute. It then sends a blank draft to player 1,
cancels a draft to player 2, sends `TO TWO` to player 2 and `M01` to `M17` to
player 1, one Send each. Player 1 views, presses Previous, Next, Next and
Previous, closes View, opens it again and sends `REPLY` to players 0 and 2.
Player 2 views, steps to its last message, presses Next once more and
Previous, and opens View again. In turn 2 player 0 reads `REPLY` and sends
`TURN TWO` to player 1 and `LATE` to player 2; player 1 views and steps
through all 13 of its messages; player 2 reads `LATE`. In turn 3 player 1
presses View.

## Observations

All records of all players start with `occupied` 0 and `read` 1 and every
other byte 0, and every count and cursor is 0.

View with an empty inbox calls the handler, plays slot 4 and does not open. The
cards of player 3 and of the sender play slot 4 and leave the selection bytes
at 0, and so does Send with nothing selected, with the panel left open. A card
of another human flips its byte. After the keys the draft's four rows hold
`AZ`, 37 spaces and `9`; `Q`, a space and `.,-/;='`; nothing; and `Y` followed
by 39 `X`. `[`, `` ` ``, `\` and `]` were not stored. Enter did not send.
Execute closed the panel and stored a copy for players 1 and 2 with `occupied`
1, `read` 0, `turn` 0, `sender` 0, the 160 characters and byte `0xA5` 0. The
blank draft closed the panel and stored nothing, and so did Cancel.

Player 1's count went up to 16 with `M15`. `M16` dropped the first message
and `M17` the next, leaving `M02` to `M17` and the count at 16.

At player 1's planning entry `comlink_pending` was 1 and slot 6 played. View
opened at record 0 and drew 1, 16, 2050 and 1. Previous at the first message
played slot 4 and showed nothing; each step played slot 3 and showed the
message, setting its `read`. View opened again at record 3, the oldest unread,
and drew 4 as the page. Done called the drop function for player 1 with 16
messages and left 12, `M06` to `M17`, unread, with the cursor at 0. Player 2
read its three messages; Next at the last played slot 4; after Previous and
closing, View opened again at record 1, where it had been, with nothing
unread. Done left player 2 with no message.

In turn 2 (`elapsed_turns` 1) `TURN TWO` and `LATE` carry `turn` 1, and View
draws week 2 of 2050 for them and week 1 for the messages of turn 1. Player 1
reached its 13th message with every step allowed. Each Done called the drop
function once for the human who pressed it, and never for a computer player.
In turn 3 every inbox was empty and View played slot 4 and did not open.

## Results

A test of the rebuild plays the same steps in a rebuild match with humans in
slots 0 to 2, through the code the game client calls for the panel's recipient
checks, the text editor with the client's key map, Send, the View cursor and
its paging, marking a message read, and finishing a command. After
every step the rebuild refuses and accepts what the original did, holds the same
messages with the same read flags, turns, senders and texts in the same order,
the same counts and View pages, the same draft, the same selection, and the same
pending flag, and drops the same number of messages at each Done. Wherever the
original played slot 6 the rebuild's planning player has an unread message
before or after the step, and at each planning entry the client's alert timing
sounds slot 6 exactly where the original did.

## Conclusion

The runs agree with RULE-COMLINK-001 to RULE-COMLINK-007, FMT-STATE-005 and
the planning-entry and repeat alerts of RULE-AUDIO-007 wherever a local game
reaches them. The characters `0x5B` and above are
dropped, as FND-COMLINK-007 and FND-COMLINK-008 read. No key event delivers a lower-case letter, since
`MapVirtualKeyA` returns capitals (FND-UI-020), so the step that turns `a` to
`z` into capitals is not reached from the keyboard. Two branches stay
unobserved: delivery to another computer, and the recorder moving a cursor
above 0 back when it drops a message, which needs the recipient's View in use
while a message arrives and so a network game.

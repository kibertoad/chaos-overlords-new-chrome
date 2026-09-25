---
id: FND-UI-021
title: The menu bar's items are greyed by game state through position and command helpers, table 102 gives six Ctrl accelerators, and menus 1, 2, 3 and 5 are gang order popups
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004252D0..0x00425843
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004120A7..0x004120EE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004650E2..0x004653AD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464AE6..0x00464B42
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F..0x00414D8B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414D8C..0x004169B2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004B0D00..0x004B15BB
tool: Ghidra 12.1.3 and a resource listing of the executable
environment: null
---

## Observation

Resources (FND-EXE-005):

- Menu 101 has five top-level entries: the File popup, a plain item with ID
  `0xFFFF` marked greyed in the resource (an Edit placeholder), and the
  Options, Comm and Help popups. The items and IDs are those of FND-UI-008.
  The resource marks Save `0x8103`, End `0x8104` and Disconnect `0x8501` greyed
  and None `0x8503` checked.
- Accelerator table 102 has six entries, all without the `FVIRTKEY` flag, so
  each matches a character code: 8 (Ctrl+H) sends `0x8106` Host, 10 (Ctrl+J)
  `0x8107` Join, 14 (Ctrl+N) `0x8101` New Game, 15 (Ctrl+O) `0x8102` Open, 18
  (Ctrl+R) `0x8104` End and 19 (Ctrl+S) `0x8103` Save. The item labels show the
  same keys.
- Menus 1, 2, 3 and 5 each hold one popup. Menu 1 lists the thirteen orders
  Attack to Snitch with IDs 1 to 13 (the order of the `action` codes without
  Terminate), None with ID 15 and Terminate with ID 17. Menu 2 lists Chaos,
  Control, Heal, Hide, Influence and Research with IDs 1 to 6 and None with ID
  8. Menu 3 lists Attack, Bribe, Chaos, Control, Heal, Hide, Influence, Move and
  Snitch with IDs 1 to 9, None with 11 and Terminate with 13. Menu 5 lists
  Chaos, Control, Heal, Hide and Influence with IDs 1 to 5 and None with 7.

Helpers:

- `fn_004252D0` empties the table of eight menu-bar groups at `0x004935D0`
  (count at `0x004935F0`); `fn_00425319(group)` appends one. `WinMain` appends
  `0x81`, `0x82`, `0x84`, `0x85` and `0x80`, so a group's index is its position
  on the bar: File 0, Edit 1, Options 2, Comm 3, Help 4.
- `fn_0042533F(group, item)` enables and `fn_0042548A(group, item)` greys. With
  `item` 0 or less they act on the bar position of `group` with
  `MF_BYPOSITION` and call `DrawMenuBar`. With `item` 1 or more and `group` below
  16 and not on the bar, they act on command `item` of loaded popup menu
  `group`. Otherwise they act on command `group * 256 + item` of the window's
  menu.
- `fn_00425601(group, item, on)` sets or clears the check mark of command
  `group * 256 + item` and redraws the bar; `fn_004255D5` only redraws it.
- `fn_0042572C(on)` detaches the window's menu (`SetMenu(NULL)`) or attaches
  the one it saved at `0x0048746C`, tracking the state at `0x00487470`. Its
  callers are `WinMain` at exit and the activation event (both attach) and the
  unreached presenter `fn_004653DE`, which detaches during its screen and
  attaches after. Nothing else detaches the menu.
- `fn_004257D7(group)` returns the popup at a group's bar position; it has no
  callers and its address appears nowhere in the file.
- `fn_004120A7` greys the whole File menu and `fn_004120CB` enables it; each has
  27 callers, the panel handlers, which grey File when they open and enable it
  when they close, and then set `0x00498100` (FND-UI-020).
- `fn_00464AE6(disable)` keeps `0x00498350` and greys Save when it is set,
  enables it otherwise; a nonzero `0x00482178` forces the greyed state.
- `fn_004650E2` clears the check marks of the four transports, checks the one
  `comm_type` names, and greys Host and Join when `comm_type` is 0 (None),
  enables them otherwise. `fn_00465193` enables all four transports and keeps
  `comm_type`; `fn_0046525B` greys all four. `fn_004652A0` checks the chosen
  Music and Sound Effects levels (commands `0x0601 + music_level` and
  `0x0701 + effects_level`) after clearing the others.

Menu states found:

| When | Greyed | Enabled | Source |
|---|---|---|---|
| Resource defaults | Save, End, Disconnect, the Edit item | the rest | menu 101 |
| Startup | Thousands of Colors unless both image sets exist and `full_screen_active` | Comm transports; Host and Join only when `comm_type` is not None | `0x0046109C`, `fn_004650E2` |
| Title New Game or Open chosen, until the game ends | the Comm menu | | `0x0046175B`, `0x00461E9B` |
| Host or Join chosen | the four transports | | `fn_0046525B` |
| Planning entered (`fn_0046E766`) | New Game, Open, Host, Join; Save when `0x00498350` is set | End; Disconnect in a network game | `0x0046EA6C..0x0046EB07` |
| Resolution in progress (`0x0046F728`) | Save | | `0x0046F73D` |
| Game over, back to the title | Save, End, Disconnect | New Game, Open, the Comm menu | `0x0046F99C..0x0046F9FA` |
| A panel open | the File menu | | `fn_004120A7` |
| About screen (`fn_00464D53`) | File, Edit, Options, Comm, Help | File, Options, Comm, Help again on leaving | `0x00464D72..0x0046506D` |
| Setup `fn_0040E0A0` and the network screens `fn_0040B9C0`, `fn_004677F0`, `fn_00456F80` and `fn_0042B9E0` | New Game, Open, Save, End, Host and Join while they run; Save and End stay greyed after | New Game, Open, Host and Join when the first four return | their calls to `fn_0042548A` and `fn_0042533F` |

The popups, `fn_0042566D(menu, slot, point)`: it takes the first popup of loaded
menu `menu`, converts `point` from client to screen coordinates, and calls
`TrackPopupMenu` with `TPM_RETURNCMD`, returning the chosen command or 0.

- The gang cards of the detailed sector screen, `fn_00414D8C(card, point,
  kind)`: card `i` has its corner at x `254 + (i mod 2) * 76`, y
  `80 + (i / 2) * 112`, and size 74 by 110. A press inside the rectangle top 8,
  left 5, bottom 17, right 69 of the card, while `0x004ABC9C` and
  `0x004ABC60` are clear, opens menu 2 when its x within the card is more than
  37 and menu 1 otherwise, at the card's corner moved 1 left and 8 down.
  Before menu 1 it greys Attack unless another player's byte `0x10 + p` of the
  gang's sector record is nonzero; Control when the sector's `owner` is -3;
  Control and Influence when the gang's player owns the sector, enabling
  Influence again when any of its three site slots differs from its site's
  figure at `0x004AB67E + site * 0x3E`; Influence when someone else or no one
  owns it; Control and Influence while `crackdown_turns` is above 0; Heal when
  the gang's `force` is 10; Sell and Give when the gang has no weapon, armor or
  misc item; and Give when only one card is filled. Menu 2 greys Control,
  Influence and Heal by the same tests. After the choice it enables them
  again. Orders 1 (Attack), 5 (Equip), 6 (Give), 9 (Influence), 10 (Move), 11
  (Research) and 12 (Sell) first run their picker panels and are dropped when
  the picker returns 0. A chosen order writes its code to `action` (None as 0,
  Terminate as 14) and clears `repeat_action` and `repeat_target`. A chosen
  default writes `repeat_action` and `action` together (Chaos 3, Control 4,
  Heal 7, Hide 8, Influence 9, Research 11, None 0), copying `target` to
  `repeat_target` for Influence and Research.
- The group bar, `fn_0041462F(defaults)`, called from `fn_00470E24` for a press
  inside top 61, left 253, bottom 77, right 405 while `0x004ABC4C` is set: x
  more than 367 opens menu 5 (defaults), otherwise menu 3 (orders), both at
  client point (290, 65). Before the popup it greys Control when the active
  player owns the sector and Influence otherwise, both while `crackdown_turns`
  is above 0, and in menu 3 Attack unless another player's byte `0x10 + p` of
  the sector record is nonzero. The choice is written to every gang of the
  active player in the selected sector (`action` and its targets, from the
  picker's scratch record in roster slot 80): Attack only after the Attack
  picker `fn_0043B290` accepts, Influence after `fn_0043F692`, Move after
  `fn_004413EF`. For Heal only gangs whose `force` is below 10 are changed.
  Group orders set `repeat_action` to 0; group defaults set it to the code.

## Interpretation

The menu bar stays attached for the whole session: the manual's menu bar that
hides during play is not in this build. Menu commands and their accelerator
keys produce the same events. The accelerators are character codes, and
Windows gives some plain keys the same codes: Backspace produces 8 and
Ctrl+Enter 10, so in a loop that runs `fn_0045C180` they act as Host and Join
when those items are enabled.

## Alternatives

Whether `TranslateAcceleratorA` sends the command of a greyed item is decided
by Windows; the documented behaviour is that it does not, and that has not been
tried here. The meaning of `owner` -3 and of the per-player bytes `0x10..0x15`
of the sector record is taken from these tests alone.

## How to reproduce

List the menu and accelerator resources of the executable. Follow the calls to
`0x0042548A` and `0x0042533F` (each pushes the group and then the item) from
`0x0046E766`, `0x00464D53` and `0x00460CCF`. The `TrackPopupMenu` call is at
`0x00425715`; its callers are at `0x004150C7`, `0x004156B7`, `0x00414739` and
`0x004149AF`.

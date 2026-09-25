---
id: FND-STATE-008
title: Map of the interface, platform and network globals in .data, with each region's element, writers, readers and identity
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482030..0x0048204B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004854C4..0x00487B98
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048FB70..0x00498877
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498938..0x00498BB9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC40..0x004ABCA3
tool: Ghidra 12.1.3
environment: null
---

## Observation

This is the second part of the data map of FND-STATE-007 and uses the same
columns and the same method. It covers the regions that hold no match state:
the window, drawing, sound, file and input globals, the panels' working values,
and the serial, modem and network state. Where a finding already describes a
region's fields, the row names the finding and does not repeat them.

| Region | Element | Writers | Only readers | What it is |
|---|---|---|---|---|
| `0x00482030..0x0048204B` | handles and flags | `fn_00401EF0`, `fn_0040209E`, `fn_00402140`, 3 more | `fn_004020EC`, `fn_00402463`, `fn_004028E9`, 3 more | serial link: the port handle, the reader thread and its state, used by `fn_00401EF0` to `fn_00402B33` |
| `0x004854C4..0x004854C7` | INT32 | `fn_0040E0A0`, `fn_0040F72E`, `fn_00410016` | `fn_0040EE8A` | `selected_card`, the setup card being edited (FND-SETUP-005) |
| `0x004854C8..0x004854CB` | INT32 | `fn_0041953E`, `fn_004196F5` | `fn_00462579` | not identified |
| `0x004854D0..0x0048626B` | modem records | `fn_0041BE20`, `fn_0041C014`, `fn_0041C0E0`, 9 more | `fn_0041C346`, `fn_0041CA2F`, `fn_0041CFCA`, 20 more | modem link: line and call handles, status and buffers of `fn_0041BE20` to `fn_0041F808` |
| `0x00487348..0x00487358` | dwords | `fn_00423C81` | `fn_004215BB`, `fn_00423F80` | network values, not identified further |
| `0x0048735C` | UINT8 | none | `fn_0045851A`, `fn_004589B8`, `fn_0045EAB1` | 0 in the image and never written; the sound and Comlink code test it (FND-AUDIO-006, FND-COMLINK-007) |
| `0x00487428..0x00487470` | dwords | `fn_0042572C` | `fn_00424FB4`, `fn_00465A95` | window values of `fn_00424FB4` and `fn_0042572C`; the saved menu at `0x0048746C` and its state at `0x00487470` (FND-UI-021) |
| `0x00487480..0x004874DF` | system colours | none | `fn_00425850`, `fn_00462579` | system colours saved at start-up (FND-GFX-004) |
| `0x004874E0..0x004874EC` | dwords | none | `fn_00425850`, `fn_00425D97`, `fn_004282AA`, 1 more | display mode values (FND-GFX-004) |
| `0x00487538..0x0048763F` | path and length | `fn_0042B8EC` | `fn_0042AFDD`, `fn_0042B27A`, `fn_00458290`, 4 more | the install directory, length-prefixed (FND-PLATFORM-010) |
| `0x00487B18` | handle | `fn_00465620` | `fn_0045C180` | the accelerator table (FND-PLATFORM-009) |
| `0x00487B58` | UINT8 | `fn_0040B9C0`, `fn_0040E0A0`, `fn_00456F80`, 5 more | `fn_0042C3F5`, `fn_0045D2F0`, `fn_0046BA84`, 4 more | `network_game` (FND-NET-001) |
| `0x00487B80..0x00487B98` | dwords | `fn_00410770`, `fn_00413858`, `fn_00460CCF`, 6 more | `fn_00411D9B`, `fn_00416C75`, `fn_00417CBA`, 10 more | view state: `0x00487B88` is 1 while the city view is shown and 0 in the sector view, `0x00487B8C` the player whose gangs the sector view lists (FND-UI-015, FND-UI-018) |
| `0x0048FB70` | UINT8 | `fn_0040B9C0`, `fn_0040DAB9`, `fn_0046BA84` | none | a Join setup flag (FND-NET-001, FND-NET-003) |
| `0x0048FB78..0x0049059D` | 0xA26 bytes | `fn_004217C0`, `fn_0046BA84` | none | network transfer buffer: one player's 81 gang records, then that player's three hire offers (`0x00490598`) and three hire orders (`0x0049059B`), sent as 2,598 bytes by `fn_0046BA84` (FND-VIDEO-002) |
| `0x004905A0..0x004905B7` | dwords | `fn_0040B9C0`, `fn_0040CED0`, `fn_004677F0`, 1 more | `fn_0040C4C5`, `fn_0046913D` | network setup: a flag, a count and four seat entries from `0x004905A8` (FND-NET-001, FND-VIDEO-002) |
| `0x004905C0..0x0049062B` | 3 records of 36 bytes | `fn_0040DAE0`, `fn_0040DBC0`, `fn_0040DD7B`, 2 more | `fn_0040E00E`, `fn_0040E02B` | Smacker overlay slots of `fn_0040DAE0` to `fn_0040E049` (FND-VIDEO-002) |
| `0x00490630..0x00490677` | 6 records of 12 bytes | `fn_00410016` | `fn_0040B9C0`, `fn_0040E0A0`, `fn_004677F0` | default player names built by `fn_00410016` (FND-SETUP-013) |
| `0x00490678..0x00490687` | dwords | `fn_0040E0A0`, `fn_00410016` | none | setup screen values (FND-SETUP-013, FND-SETUP-017) |
| `0x00490698..0x004906A7` | dwords | `fn_00412BF7`, `fn_0041B8BC`, `fn_0041BCBC`, 2 more | `fn_0041B8FC`, `fn_0041BDD5` | the planning timer of `fn_0041B8BC` (FND-UI-017) |
| `0x004906A8..0x004906C3` | dwords | `fn_0041CFCA`, `fn_0041D0A5`, `fn_0041D2CB`, 2 more | `fn_0041C36E`, `fn_0041C735` | modem timers |
| `0x004906D0..0x00492E8F` | 12 records of 0x350 bytes | `fn_004211E0`, `fn_004215BB`, `fn_00421A2D`, 17 more | `fn_004217C0`, `fn_0042202D`, `fn_004222C6`, 6 more | network connections: +0x18 the socket, +0x190 to +0x195 event flags that window message 0x401 sets (FND-NET-004) |
| `0x00492F80..0x004933B7` | Winsock state | `fn_004211E0`, `fn_004214BA`, `fn_004215BB`, 5 more | `fn_004217C0`, `fn_0042202D`, `fn_004222C6`, 7 more | WSADATA at `0x00493058`, host name at `0x00492F90`, 32-byte name records at `0x00493220`, listen flags at `0x004933B0` (FND-NET-004) |
| `0x004935D0..0x004935EF` | INT32[8] | `fn_004252D0`, `fn_00425319` | `fn_0042533F`, `fn_0042548A`, `fn_004257D7` | menu command table (FND-UI-021) |
| `0x004935F0..0x004935FF` | handles | `fn_004252D0`, `fn_00425319`, `fn_00425850`, 1 more | `fn_00426202`, `fn_00426427`, `fn_004264D4`, 7 more | pens and brushes (FND-GFX-004) |
| `0x00493658..0x00493867` | 12 records of 44 bytes | `fn_00425850`, `fn_00425FB0`, `fn_00426202`, 3 more | `fn_0040DBC0`, `fn_0040DDFF`, `fn_0042533F`, 34 more | window and surface slots (FND-GFX-004) |
| `0x00493868..0x004938DF` | dwords | `fn_00425850`, `fn_00425D97`, `fn_00425FB0`, 11 more | none | drawing state and the 24 system colours at `0x00493878` (FND-GFX-004) |
| `0x00493A30..0x00493E2F` | 256 four-byte entries | none | `fn_004282AA` | the palette `fn_004282AA` reads |
| `0x00493E30..0x00493F0B` | font record | `fn_00425850` | none | the font `fn_00425850` creates: LOGFONT at `0x00493ECC`, face name at `0x00493EEC` (FND-GFX-004) |
| `0x00493F88..0x004944F7` | 4 records of 348 bytes | `fn_0042AB80`, `fn_0042AC7A`, `fn_0042ADE9`, 3 more | `fn_0042ABFB`, `fn_0042AE85`, `fn_0042AF31`, 2 more | file slots of `fn_0042AB80` to `fn_0042B7F3` (FND-PLATFORM-010, FND-DATA-007) |
| `0x00494500..0x00494577` | 20-byte records | `fn_0042B9E0` | `fn_0042CE61` | the awards table (FND-AWARDS-004) |
| `0x00494578..0x004947C8` | Detailed Combat state | `fn_0042E040`, `fn_0043087E`, `fn_00430C23` | `fn_0042EE46`, `fn_0042F779`, `fn_0042F98B`, 1 more | Detailed Combat (FND-COMBAT-009, FND-COMBAT-010, FND-COMBAT-011); its fields are named there |
| `0x004947F8..0x004947FF` | dwords | `fn_0042E040`, `fn_00430C23` | none | Detailed Combat values of `fn_0042E040` and `fn_00430C23` (FND-COMBAT-011) |
| `0x00494800..0x00494817` | dwords | `fn_004327C0`, `fn_004327DC`, `fn_0043287C`, 2 more | `fn_00432847`, `fn_00432897`, `fn_004328BE` | timer ids and flags of `fn_004327C0` to `fn_00432926` (FND-TIMER-002) |
| `0x00494838..0x0049484F` | dwords | `fn_00445A4F` | `fn_00448027` | Give panel state (FND-GIVE-001) |
| `0x00494850..0x00494867` | dwords | `fn_0043D132` | `fn_0043B290` | Attack panel state (FND-ATTACK-003) |
| `0x00494868..0x0049488F` | flags | `fn_00449B20`, `fn_0044FD6C`, `fn_0046FD80` | `fn_00427E60`, `fn_00471F06` | report and events panel flags (FND-EVENT-005) |
| `0x00494890..0x004948A7` | dwords | `fn_00453087` | `fn_00451F80` | combat results panel state (FND-COMBAT-007) |
| `0x004948A8..0x004948E7` | rows | `fn_0043F136`, `fn_004437E7` | `fn_0043DAD9`, `fn_004427FA` | Equip and Research list rows (FND-EQUIP-008, FND-RESEARCH-003) |
| `0x004948E8..0x004948FF` | dwords | `fn_0044FD6C`, `fn_00451602`, `fn_00451F80`, 2 more | `fn_0044F2FC`, `fn_00453087`, `fn_00453A8D` | combat results and events panel state (FND-COMBAT-007, FND-EVENT-005) |
| `0x00494900..0x00494AEF` | rows | `fn_0043F136`, `fn_004437E7` | `fn_0043EFE5` | Equip and Research list rows (FND-EQUIP-008, FND-RESEARCH-003) |
| `0x00494AF0..0x00494BF3` | dwords | `fn_00451F80` | `fn_00453087`, `fn_00453A8D` | combat results panel state (FND-COMBAT-007, FND-COMBAT-012) |
| `0x00494BF4..0x00494C1F` | dwords | `fn_00458155`, `fn_00458290`, `fn_00458B43`, 3 more | `fn_0045851A`, `fn_00458895`, `fn_00458ACC`, 5 more | CD audio and MCI state (FND-AUDIO-006, FND-AUDIO-007) |
| `0x00494C28..0x00497FE7` | 48 records of 0x114 bytes | `fn_00458290`, `fn_0045867C`, `fn_00458895` | `fn_0045851A` | sound slots: +0 in use, +0x0A the path, +0x108 the memory handle, +0x10C the data, +0x110 the length (FND-AUDIO-006) |
| `0x00497FE8..0x00497FFF` | MCI block | `fn_00458290`, `fn_00458EA6` | `fn_00458E2F`, `fn_00458E68` | MCI parameters (FND-AUDIO-007) |
| `0x00498000..0x0049809F` | 8 records of 20 bytes | `fn_00458290`, `fn_004584BA`, `fn_0045851A` | `fn_00458895` | CD track records (FND-AUDIO-006) |
| `0x004980A0..0x004980BF` | MCI blocks | `fn_00458B43`, `fn_00458EA6` | `fn_00458C5F` | MCI parameters (FND-AUDIO-007) |
| `0x004980C0..0x004980FF` | PAINTSTRUCT | none | `fn_0045CD70`, `fn_0045CDA4` | the paint structure of `fn_0045CD70` and `fn_0045CDA4` |
| `0x00498100..0x00498125` | dwords | `fn_0043B290`, `fn_0043DAD9`, `fn_0043F692`, 24 more | `fn_0045D2F0`, `fn_0045FDF1`, `fn_0046023C`, 1 more | the open panel's state and the Comlink draft (FND-UI-013, FND-COMLINK-006) |
| `0x004981C8..0x004981F7` | dwords | `fn_0045D2F0`, `fn_0045D61A`, `fn_0045E7CE`, 2 more | `fn_0045E04D` | Comlink panel state (FND-COMLINK-001) |
| `0x004981F8..0x004981FF` | dword | `fn_00460CCF` | `fn_0041953E`, `fn_004196F5` | a value WinMain writes and `fn_0041953E` and `fn_004196F5` read (FND-TIMER-002) |
| `0x00498200..0x004982FF` | path | none | `fn_00460CCF` | the save file path (FND-PLATFORM-010, FND-SAVE-002) |
| `0x00498300..0x0049835F` | bytes | `fn_00460CB5`, `fn_00460CCF`, `fn_00462579`, 4 more | `fn_00425850`, `fn_00425D97`, `fn_004396C0`, 4 more | preferences read from a save (FND-SAVE-001), the M10W block at `0x00498330` (FND-SAVE-002) and flags to `0x00498354` (FND-UI-021) |
| `0x00498360..0x0049836F` | 4 dwords | `fn_0045C180`, `fn_0045C33B` | none | the current input event: type, window index, x or key, y (FND-UI-020) |
| `0x00498370..0x0049846F` | bytes | none | `fn_00465EC6` | dialog text (FND-UI-022) |
| `0x00498470..0x0049856F` | bytes | `fn_0040F63D`, `fn_0042364F` | `fn_00465EC6` | name entry buffer (FND-UI-022) |
| `0x00498570..0x00498577` | handle | `fn_00425850` | `fn_00402242`, `fn_00402A07`, `fn_004215BB`, 17 more | the main window (FND-UI-020) |
| `0x00498578..0x00498597` | MSG | none | `fn_0045C180`, `fn_0045C2CD` | the message loop's message |
| `0x00498598..0x004985A7` | dwords | `fn_0045C33B`, `fn_00465620`, `fn_00465B64` | none | pointer position and buttons (FND-UI-020) |
| `0x004985A8..0x004985D7` | WNDCLASS | `fn_00465620` | none | the window class (FND-PLATFORM-009) |
| `0x004985D8..0x00498657` | bytes | `fn_00465620` | `fn_0045C33B` | command line copy (FND-PLATFORM-009) |
| `0x00498658..0x0049875F` | 260 bytes | none | `fn_0046638E` | a path of `fn_0046638E` (FND-PLATFORM-012) |
| `0x00498760..0x0049876F` | bytes | `fn_0046638E` | `fn_004329C0` | CD drive root (FND-PLATFORM-012) |
| `0x00498864..0x00498877` | handles | `fn_00465620`, `fn_00465BC8` | `fn_0042533F`, `fn_0042548A`, `fn_0042566D`, 4 more | cursor, instance and menus (FND-PLATFORM-009) |
| `0x00498938..0x0049894F` | INT32[6] | `fn_00456F80`, `fn_0046BA84` | `fn_004677F0` | per-connection values (FND-NET-001) |
| `0x00498968..0x0049897F` | 24 bytes | `fn_004677F0` | `fn_00456F80` | the network block of an N40W save (FND-SAVE-001) |
| `0x00498980..0x0049898F` | 4 dwords | `fn_0046BA84`, `fn_0046D00D` | none | the network message header of `fn_0046BA84` and `fn_0046D00D` |
| `0x00498BB8..0x00498BB9` | bytes | `fn_00456F80`, `fn_004677F0`, `fn_0046A7CB`, 1 more | none | network turn flags |
| `0x004ABC40..0x004ABC4C` | UINT8[9] and a flag | `fn_00410770`, `fn_00411119` | `fn_00414D8C`, `fn_00416C75`, `fn_004413EF`, 1 more | sector view state: the nine neighbour flags `fn_00411119` writes and a flag `fn_00410770` writes (FND-UI-015, FND-UI-018) |
| `0x004ABC60..0x004ABC7F` | flag and dwords | `fn_00410770`, `fn_0046E766`, `fn_0046FD80`, 1 more | `fn_00414D8C`, `fn_004169B3`, `fn_00416C75`, 2 more | city view state (FND-UI-018, FND-UI-036) |
| `0x004ABC80..0x004ABC83` | INT32 | `fn_00411DF5`, `fn_0046FD80`, `fn_00470A34`, 1 more | `fn_00411D9B`, `fn_0041462F`, `fn_00414D8C`, 8 more | the selected sector (FND-UI-018) |
| `0x004ABC84..0x004ABC87` | INT32 | `fn_0046E766` | `fn_00410130`, `fn_00410770`, `fn_00411D9B`, 22 more | `active_player` (FND-UI-036) |
| `0x004ABC88..0x004ABCA3` | bytes | `fn_0040CED0`, `fn_00417CBA`, `fn_004396C0`, 9 more | `fn_00410130`, `fn_00414D8C`, `fn_0041B4EA` | turn loop flags, among them the end flag at `0x004ABC90` and the hot-seat flag at `0x004ABC98` (FND-TURN-006) |

The regions up to `0x00487B98` lie in the initialized part of `.data` and
have values in the image; the regions from `0x0048FB70` on lie in the
uninitialized part.

The event record at `0x00498360` is filled by the window procedure
`fn_0045C33B`, which `fn_0045C180` wraps. The event types it stores are 1 for
a menu command, 2 for a key press (the third field is the virtual key), 3, 4
and 5 for a left button press, release and double-click, 6 for a close
request, 7 for a repaint, 8 and 9 for the two cases of the application activation message, 0x10 for the window being destroyed, and 0x11, 0x12 and 0x13 for
the right button's press, release and double-click.

## Interpretation

Nothing in these regions reaches the rules: they hold what the game draws,
plays and reads from the player, and the link to other computers. The only
places where they touch the match state are the network transfer buffer
(`0x0048FB78`), which is a copy of one player's gang records, hire offers and
hire orders, and the network block of a save (`0x00498968`).

## Alternatives

Several small regions are named only by the functions that use them
(`0x004854C8`, `0x00487348`, `0x004906A8`, `0x00498BB8`). They belong to the
modem and network code, which DEV-NET-001 leaves out, and were not read
further.

## How to reproduce

As in FND-STATE-007. For the event types, read the message switch of
`fn_0045C33B` and the stores into `0x00498360..0x0049836C`.

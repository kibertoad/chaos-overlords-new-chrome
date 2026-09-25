---
id: FND-PLATFORM-001
title: Rendering imports combine DirectDrawCreate with GDI palette, DIB and blit calls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE568..0x004AE56C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE570..0x004AE5F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE7D0..0x004AE8E8
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `DDRAW.dll` supplies one import, `DirectDrawCreate`, at slot `0x004AE568`.
- `GDI32.dll` supplies 34 imports at `0x004AE570..0x004AE5F8`, among them
  `SetDIBits`, `BitBlt`, `StretchBlt`, `SetStretchBltMode`, `PatBlt`,
  `CreatePatternBrush`, `SetBkColor`, `CreatePalette`, `SetPaletteEntries`,
  `ResizePalette`, `SelectPalette`, `RealizePalette`, `UpdateColors`,
  `SetSystemPaletteUse`, `GetSystemPaletteEntries` and `GetDeviceCaps`.
- `USER32.dll` supplies 70 imports at `0x004AE7D0..0x004AE8E8`, covering the
  cursor (`LoadCursorA`, `SetCursor`, `ShowCursor`, `GetCursorPos`), bitmaps
  (`LoadBitmapA`), menus (`LoadMenuA`, `TrackPopupMenu`, `CheckMenuItem`,
  `EnableMenuItem`), dialogs, window creation, key state (`GetKeyState`,
  `GetAsyncKeyState`), painting (`BeginPaint`, `EndPaint`, `InvalidateRect`),
  display mode changes (`ChangeDisplaySettingsA`) and the message loop
  (`GetMessageA`, `PeekMessageA`, `DispatchMessageA`).

## Interpretation

The original draws through DirectDraw together with GDI device contexts, and
manages the palette itself. The choice between the 8-bit and 16-bit image sets
is made in this layer (FND-PLATFORM-002).

## Alternatives

Which drawing work goes through DirectDraw and which through GDI has not been
traced call by call. The imports alone do not show it.

## How to reproduce

List the import address table of `Chaos Overlords.exe`: `DDRAW.dll` at
`0x004AE568`, `GDI32.dll` from `0x004AE570`, `USER32.dll` from `0x004AE7D0`.

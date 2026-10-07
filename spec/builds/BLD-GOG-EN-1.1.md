---
id: BLD-GOG-EN-1.1
title: Chaos Overlords 1.1, English, GOG release
superseded_by: []
publisher_version: "1.1"
developer: Stick Man Games
publisher: New World Computing
distribution: GOG
languages: [en]
int_width: 32
manifest: BLD-GOG-EN-1.1.files.yaml
---

## Obtaining

Sold by GOG.com as "Chaos Overlords" (product ID 1207659228). The installer
places the game in a directory of the buyer's choosing, by default
`C:\GOG Games\Chaos Overlords`. The executable reports version 1.1, the last
official release.

## Compared with other builds

The research by SRC-RECHAOS-3561D41 was done on a Windows 95 executable of the
same game whose SHA-256 is
`0791e6209d573a79882675d1236737f5c9b369ea4af541a7dbd03cbadf4493d5`. That file
differs from `Chaos Overlords.exe` in this build, and its size and xxh3 are not
known here, so it has no build entry. Addresses from that research do not apply
to this build.

The original CD release played music from CD audio tracks. This build ships the
same music as Ogg Vorbis files under `MUSIC/`, one per track from `Track02.ogg`
to `Track09.ogg`, and a replacement `winmm.dll` that serves the executable's CD
audio requests from them.

## Other files

These files are installed with the game and are not game data:

- GOG's launcher and store integration: `goggame-1207659228.dll`,
  `goggame-1207659228.hashdb`, `goggame-1207659228.ico`,
  `goggame-1207659228.info`, `goggame-galaxyFileList.ini`,
  `GameuxInstallHelper.dll`, `gog.ico`, `Support.ico`,
  `Launch Chaos Overlords.lnk`, `webcache.zip`.
- The installer's uninstaller: `unins000.exe`, `unins000.dat`, `unins000.msg`.
- Compatibility shims: `winmm.dll` (CD audio from the Ogg files, with
  `libogg-0.dll`, `libvorbis-0.dll` and `libvorbisfile-3.dll`), and IPXWrapper
  0.4.0 for the IPX network mode (`ipxwrapper.dll`, `ipxconfig.exe`,
  `wsock32.dll`, `mswsock.dll`, `dpwsockx.dll`, `ipxwrapper-0.4.0-src.zip`,
  `ipxwrapper-license.txt`, and the log it writes, `ipxwrapper.log`).
- Documents: `Chaos Overlords - Manual.pdf` (SRC-MANUAL-GOG), `readme.pdf` and
  `EULA.txt`.

## Code ranges

None.

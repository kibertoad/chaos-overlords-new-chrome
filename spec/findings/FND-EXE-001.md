---
id: FND-EXE-001
title: The executable is a stripped 32-bit PE for the Windows GUI subsystem with image base 0x00400000
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00400080..0x00400178
tool: Ghidra 12.1.3
environment: null
---

## Observation

The PE signature sits at file offset `0x80`, which the DOS header's `e_lfanew`
field gives. The COFF file header that follows holds:

- `Machine` `0x014C` (Intel 386 or later);
- `NumberOfSections` 6;
- `TimeDateStamp` `0x31EFE176` (837804406), which is 1996-07-19 19:26:46 UTC;
- `PointerToSymbolTable` 0 and `NumberOfSymbols` 0;
- `SizeOfOptionalHeader` `0xE0`;
- `Characteristics` `0x010E`: executable image, COFF line numbers stripped,
  local symbols stripped, 32-bit machine. The relocations-stripped flag is
  clear.

The optional header, from `0x00400098`, holds:

- `Magic` `0x010B` (PE32);
- linker version 3.10;
- `AddressOfEntryPoint` giving the entry point `0x00478D00`;
- `ImageBase` `0x00400000`;
- `SizeOfImage` `0x000C9000`;
- `Subsystem` 2 (Windows GUI), operating system and subsystem version 4.0;
- `CheckSum` 0;
- four non-empty data directories: imports at RVA `0xAE000` (`0xDC` bytes),
  resources at RVA `0xB0000` (`0x11BEC` bytes), base relocations at RVA
  `0xC2000` (`0x5BFC` bytes) and the import address table at RVA `0xAE554`
  (`0x464` bytes). The debug directory is empty.

## Interpretation

`Chaos Overlords.exe` is a native 32-bit Windows program, built in July 1996,
with no debug information and no symbols. The addresses in this spec are
virtual addresses with the file loaded at `0x00400000`. The GOG release did not
replace the executable with a launcher of its own: this file is the game.

## Alternatives

The timestamp is whatever the linker wrote and could in principle have been
changed later. Nothing in the file suggests that it was.

## How to reproduce

Open `Chaos Overlords.exe` in Ghidra or any PE dumper and read the headers at
file offset `0x80`. The entry point is `0x00478D00`.

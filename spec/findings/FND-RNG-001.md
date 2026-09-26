---
id: FND-RNG-001
title: The generator is seeded once per process from the low 16 bits of timeGetTime, and the preference loader may then draw twice
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465620
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00478CC0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A
tool: Ghidra 12.1.3
environment: null
---

## Observation

The import table lists `GetTickCount`, `timeGetTime`, the periodic multimedia
timer functions and the asynchronous key-state function. No C runtime DLL is
imported, so the C runtime, and its random number generator with it, is linked
into the executable.

Process initializer `fn_00465620` calls `timeGetTime` at `0x004658FB`, keeps
only the low 16 bits of the result (the AX half of EAX, zero-extended), and
passes that value to `fn_00478CC0` at `0x00465905`. `fn_00478CC0` fetches the
calling thread's C runtime data block and stores its only argument into that
block's random state (the runtime's `_holdrand`). `fn_00478CC0` has no other
caller.

The startup function `fn_00460CCF` calls `fn_00465620` at `0x00460CF7` and then
the preference loader `fn_0046439A` at `0x00460D14`. The loader reads its
registry values into one shared DWORD buffer and reads `serialNum` right after
`prefsFullScreen`. Only when the buffer holds 0 after the `serialNum` query do
the calls at `0x00464726` and `0x00464739` each ask the bounded wrapper
`fn_0045D227` for a value from 1 to 16384. One is subtracted from each result
and the two are combined into a serial number, which the loader tries to write
back to the registry. The key was opened with access mask `0x20019`
(`KEY_READ`), and the result of the write is ignored.

## Interpretation

`fn_00478CC0` is the runtime's `srand`. The game seeds its generator once per
process with `timeGetTime() & 0xFFFF`, a value from 0 to 65535, and never
reseeds it: starting, loading or finishing a match does not touch the state.
The other clock imports serve timing, presentation or networking; none of them
feeds the generator's state.

Each bounded request makes three raw draws (FND-RNG-003), so startup makes
either no draws or six after the seed and before any menu. Which one depends on
the value the shared buffer holds after the `serialNum` query: a failed query
leaves what the previous query left, so a missing serial number leads to the
draws only when that earlier value is 0. The write through the read-only handle
cannot succeed, so a missing serial number stays missing and the same test is
made at every launch.

## Alternatives

The random state lives in per-thread runtime data. If the game's draws were
made on a thread other than the one that runs `fn_00465620`, that thread would
start from the runtime's default state of 1 instead of the seed. No second
thread that draws has been found, but the thread each caller of the bounded
wrapper `fn_0045D227` runs on has not been recorded.

Whether any code other than `fn_00478CC0` and the runtime's `rand` writes the
per-thread random state directly has not been checked beyond the incoming
references of `fn_00478CC0`.

How the two serial number draws are combined has not been recorded. That a
failed `RegQueryValueExA` leaves the buffer unchanged is the documented
behaviour of the Windows function, not an observation of this build.

## How to reproduce

Open `Chaos Overlords.exe` in Ghidra and follow the references to the
`timeGetTime` import. One of them is the call at `0x004658FB` in
`fn_00465620`; the next call, at `0x00465905`, goes to `fn_00478CC0`. List the
references to `fn_00465620` to reach the call at `0x00460CF7` in
`fn_00460CCF`, and the references to `fn_00478CC0` to see that there is only
one. The next call in `fn_00460CCF`, at `0x00460D14`, enters `fn_0046439A`;
the query that passes the string `serialNum` is followed by a test of the
buffer against 0 and the two calls to `fn_0045D227`, each with the argument
16384.

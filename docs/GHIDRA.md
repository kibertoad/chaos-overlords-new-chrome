# Ghidra setup for clean-room analysis

This project uses Ghidra only as a research tool against a legally owned local
copy of *Chaos Overlords*. Ghidra projects, proprietary binaries, byte dumps,
and full disassembly/decompiler output must never be added to Git.

## Known local installation

Quick lookup: Ghidra is pinned at
`C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC` and its
JDK at `C:\Users\kiber\AppData\Local\Programs\Java\jdk-21.0.12.1+1`. Check
these exact paths first; do not search the machine or download replacements
while both documented launchers exist. Temporary analysis projects live under
`%TEMP%\rechaos-ghidra-*` and are disposable, so enumerate that narrow pattern
when looking for a reusable project rather than searching the filesystem.

The current Windows research machine has:

| Tool | Version | Location |
| --- | --- | --- |
| Ghidra | 12.1.3 | `C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC` |
| Eclipse Temurin JDK | 21.0.12.1 | `C:\Users\kiber\AppData\Local\Programs\Java\jdk-21.0.12.1+1` |

Before downloading anything, verify that
`support\analyzeHeadless.bat` and `bin\java.exe` exist at those locations.
Isolated processes may not inherit the user's environment, so set `GHIDRA_HOME`,
`JAVA_HOME`, and prepend both tool directories to that process's `PATH`.

Ghidra may need to persist its selected Java home and preferences below the
user profile. A sandboxed process that cannot write those settings will fail
before analysis; rerun the same narrowly scoped analyzer command with the
required user-level permission rather than reinstalling either tool.

## Reference executable

The supported local oracle is:

```text
C:\GOG Games\Chaos Overlords\Chaos Overlords.exe
```

- Version: 1.1
- Length: 664,576 bytes
- SHA-256: `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`
- Format: 32-bit x86 Windows PE

Always verify the length and hash before interpreting an address. Findings from
another executable version must be tracked separately.

## Reference manual

The canonical local manual used for rule transcription is:

```text
C:\GOG Games\chaos_overlords_manual\Chaos Overlords - Manual.pdf
```

- Length: 6,229,841 bytes
- SHA-256: `bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`
- Layout: 30 PDF pages containing 56 numbered scan pages

The copy at `C:\GOG Games\Chaos Overlords\Chaos Overlords - Manual.pdf` has
the same length and SHA-256 and is therefore byte-identical. Rule evidence
should cite `MANUAL-GOG-1` in `GAME-RULES.md`; local absolute paths are setup
metadata, not runtime dependencies.

## Headless workflow

Create every project in a unique directory under `%TEMP%` and import the owned
executable directly:

```powershell
$rechaosGhidraHome = 'C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC'
$rechaosJavaHome = 'C:\Users\kiber\AppData\Local\Programs\Java\jdk-21.0.12.1+1'
$env:GHIDRA_HOME = $rechaosGhidraHome
$env:JAVA_HOME = $rechaosJavaHome
$env:Path = "$rechaosJavaHome\bin;$rechaosGhidraHome;$rechaosGhidraHome\support;$env:Path"
$rechaosProjectRoot = Join-Path $env:TEMP ('rechaos-ghidra-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $rechaosProjectRoot | Out-Null
& "$rechaosGhidraHome\support\analyzeHeadless.bat" `
  $rechaosProjectRoot RechaosAnalysis `
  -import 'C:\GOG Games\Chaos Overlords\Chaos Overlords.exe' `
  -overwrite
```

Reuse the resulting temporary project for focused scripts without reimporting:

```powershell
& "$rechaosGhidraHome\support\analyzeHeadless.bat" `
  $rechaosProjectRoot RechaosAnalysis `
  -process 'Chaos Overlords.exe' `
  -noanalysis `
  -scriptPath "$PWD\tools\ghidra" `
  -postScript ReportRandomnessCandidates.java
```

`ReportRandomnessCandidates.java` reports candidate timing/random imports and
the containing functions that reference them. Its output is navigation
metadata, not proof of an RNG algorithm. Decompile and trace each candidate,
then confirm it against controlled original-game observations before changing
compatibility logic.

For a small, explicitly selected set of virtual addresses, request focused
decompiler output with:

```powershell
& "$rechaosGhidraHome\support\analyzeHeadless.bat" `
  $rechaosProjectRoot RechaosAnalysis `
  -process 'Chaos Overlords.exe' -noanalysis `
  -scriptPath "$PWD\tools\ghidra" `
  -postScript ReportFunctionSummary.java 0x00478cd0 0x0045d227
```

Use this only for bounded navigation and interpretation. Do not redirect broad
decompiler output into the repository.

`ReportReferences.java` reports references to explicitly supplied addresses and
their containing functions. `ReportDataBytes.java` prints at most 256 bytes at
an explicitly supplied virtual address. Both are navigation aids for small,
reviewable questions; their output must not be committed.

`ReportScalarConstants.java` accepts explicit decimal or `0x`-prefixed scalar
values and reports at most 300 instructions containing them. Use it to locate a
small known resource id or timing constant, never as an unrestricted dump.

`ReportDecompileMatches.java` accepts one function address followed by literal
text patterns and emits at most 240 lines with two lines of context. Use it to
answer a narrow question inside a large function without retaining or
committing the complete decompiler listing.

After locating relevant line numbers with that script,
`ReportDecompileWindow.java` accepts one function address, a one-based start
line, and a line count from 1 through 160. Use it to follow only an explicitly
selected basic-block-sized window; never stitch adjacent windows together to
reconstruct or retain a complete function.

`ReportStringReferences.java` accepts explicit case-insensitive string
fragments, reports at most 100 matching defined strings, and reports at most
100 references per match. Use it to navigate from a known UI label or error
message to the small set of functions that consume it; never use an empty or
generic fragment to inventory the executable's text.

`ReportSymbolReferences.java` performs the same bounded navigation for one or
more explicit symbol-name fragments, such as a known imported API.
`ReportInstructionContext.java` accepts explicit instruction addresses and
prints at most eight instructions on either side without crossing the
containing function. Use these together to classify a narrow call site or
scalar hit before requesting any decompiler text.

`ReportCallSitesWithScalars.java` accepts one callee address followed by exact
scalar values and reports only calls whose preceding 12-instruction argument
setup (bounded by the previous call) contains one of those values. Use it to
distinguish a known resource ID from unrelated occurrences of the same small
integer, then confirm the actual argument position in the emitted context.

## Evidence discipline

- Record executable hash, Ghidra version, virtual address, call relationship,
  observed constants, and an independent behavioral description.
- Never copy decompiled implementation into production. Reimplement factual
  behavior independently using project naming and structure.
- Store stable findings in `ORIGINAL-INTERNALS.md` and rules in `GAME-RULES.md`.
- Mark an interpretation Provisional until static evidence and a controlled
  observation agree.
- Do not commit temporary Ghidra projects, proprietary resources, executable
  bytes, full disassemblies, or decompiler dumps.

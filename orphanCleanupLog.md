# Orphan cleanup log

## 2026-09-12 23:23:17 +03:00

Stopped ten orphaned `dotnet` processes left by two interrupted full test runs:

- 23:00:11 batch: PIDs 19536, 31688, 10516, 17700, 16408, and 22188.
- 23:13 test-runner PID 23944 and its 23:13:43 batch: PIDs 14332, 27200, and
  2708.

The processes remained after their parent test sessions had been interrupted
and together retained roughly 1.5 GB of working memory. No Ghidra or Java
process was running. The active final validation pair (PowerShell PID 14196 and
`dotnet` PID 9940, started 23:21:11) was deliberately left running. An older
`dotnet` PID 8560, started 22:17:33 and not proven to belong to an interrupted
repository task, was also left untouched.

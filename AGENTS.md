# Repository agent instructions

## Git push destination

The authorized canonical repository is
`https://github.com/kibertoad/chaos-overlords-new-chrome.git`.
Before every push, inspect the repository's configured push destination with
`git remote get-url --push origin` (and `git remote -v` when additional context
is useful), and verify that it resolves to this canonical repository. Push
through the configured remote name and an explicit refspec, for example
`git push origin HEAD:main`.

Never rewrite, replace, or temporarily override a remote URL in order to push.
This prohibition includes `git remote set-url`, changing `remote.*.url` or
`remote.*.pushurl`, and command-scoped configuration such as
`git -c remote.origin.pushurl=...`. If the configured destination is missing or
does not match the repository the user authorized, stop and ask the user to
correct or approve the remote configuration instead of modifying it.

## Validation scope

Do not run the full test suite by default. It contains deliberately separated
long-running campaign coverage and takes too long for routine changes. Use the
default fast gate in `tools/Invoke-Validation.ps1`, or pass `-TestFilter` for a
smaller relevant scope. Run with `-IncludeLongRunningTests` or otherwise execute
the full suite only when the user explicitly requests it or when a specific
change to long-running coverage provides a documented exceptional reason.

On Windows, the PowerShell execution policy may reject repository scripts and
the `pnpm.ps1` command wrapper. Invoke the fast gate explicitly with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Invoke-Validation.ps1
```

Pass validation arguments after the script path when needed. Run pnpm-based
validators through `pnpm.cmd` (for example,
`pnpm.cmd --filter @chaos-overlords/contracts test:run`) so PowerShell does not
select the blocked `pnpm.ps1` wrapper.

## Multiplayer protocol version

Keep `MULTIPLAYER_PROTOCOL_VERSION` in
`multiplayer/packages/contracts/src/protocol.ts` and `MultiplayerProtocolVersion.Current` in
`src/Rechaos.Multiplayer/Protocol/MultiplayerProtocolVersion.cs` equal. Whenever a change can affect
communication between the game client and coordination server—including request or response
schemas, routes, authentication, event streams, serialization, or protocol behavior—increment both
versions in the same change. Never update only one side.

## Post-commit orphan-process audit

After every commit in this repository, inspect running processes for orphaned
work created by this repository's tasks. Check at least PowerShell
(`powershell` and `pwsh`), Ghidra/Java, .NET (`dotnet` and `testhost`), and any
other process families that the agent launched while building, testing,
validating, or analyzing this repository.

Reusable MSBuild nodes (`dotnet` running `MSBuild.dll` with `/nodeReuse:true`)
are expected background workers, not orphans. Do not stop or log them merely
because their spawning build process exited, their start time matches a
repository validation, or they remain idle after a build. They are exempt from
cleanup unless there is separate evidence that the process is malfunctioning
and must be stopped to complete this repository's work. Do not disable MSBuild
node reuse in routine build or test commands; keeping these workers available
makes subsequent builds faster.

Multiple conversion agents normally work in parallel, but they work on other
games. A process may be treated as belonging to this work when its command line,
parent, task/session, or source paths show that it targets this repository or
the reference installation/assets at `C:\GOG Games\Chaos Overlords`. Do not
terminate a process merely because its executable name matches. Preserve
processes for other games, unrelated user/IDE/system processes, and validation
that is intentionally still running. If repository/source ownership is
uncertain, leave the process running.

Stop confirmed orphaned repository processes. Whenever any process is stopped,
append an entry to `orphanCleanupLog.md` containing:

- the local timestamp including UTC offset;
- each stopped PID and process name;
- its start time or task/session association when known;
- why it was identified as orphaned;
- any related process deliberately left running and why.

If the audit finds nothing to stop, no log entry is required.

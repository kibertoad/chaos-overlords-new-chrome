// The transcript the host tests replay: a match the native build played, written by
// tools/ResolverDeterminism. RESOLVER_TRANSCRIPT names one already written; otherwise the global
// setup writes one with the .NET SDK (DOTNET names the executable), once per run. Both need the
// bundle (`pnpm bundle`), so without it, or without the SDK, the tests that replay are skipped,
// unless REQUIRE_RESOLVER is set, as it is in CI, where a skip would hide a resolver nobody tested.
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const packageDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const repositoryRoot = path.resolve(packageDir, '..', '..', '..')

/** Whether `bundle/` has been laid out. */
export function bundleExists() {
  return fs.existsSync(path.join(packageDir, 'bundle', 'manifest.json'))
}

/** The vitest global setup: writes the transcript unless one is named, and says why there is none. */
export default function writeTranscript() {
  if (process.env.RESOLVER_TRANSCRIPT) return undefined
  if (!bundleExists()) {
    process.env.RESOLVER_MISSING = 'there is no resolver bundle; run `pnpm bundle`'
    return undefined
  }
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'rechaos-transcript-'))
  const file = path.join(directory, 'transcript.json')
  const project = path.join(repositoryRoot, 'tools', 'ResolverDeterminism')
  try {
    execFileSync(
      process.env.DOTNET || 'dotnet',
      ['run', '--project', project, '-c', 'Release', '--', file],
      {
        cwd: repositoryRoot,
        stdio: ['ignore', 'ignore', 'pipe'],
      },
    )
    process.env.RESOLVER_TRANSCRIPT = file
  } catch (error) {
    process.env.RESOLVER_MISSING = `the .NET SDK could not write a transcript: ${error.message}`
  }
  return () => fs.rmSync(directory, { recursive: true, force: true })
}

/**
 * The transcript, or why there is none.
 *
 * @returns {{ transcript?: any, missing?: string }}
 */
export function loadTranscript() {
  if (!bundleExists()) return { missing: 'there is no resolver bundle; run `pnpm bundle`' }
  const file = process.env.RESOLVER_TRANSCRIPT
  if (!file) return { missing: process.env.RESOLVER_MISSING ?? 'no transcript was written' }
  return { transcript: JSON.parse(fs.readFileSync(file, 'utf8')) }
}

/** Whether a missing bundle or transcript must fail the run rather than skip. */
export const resolverRequired = process.env.REQUIRE_RESOLVER === '1'

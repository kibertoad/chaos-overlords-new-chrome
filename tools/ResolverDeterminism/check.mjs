// Holds the WebAssembly resolver to the native build's hashes, through the hosts the coordination
// server runs it in: the Node host (a worker thread) and the Cloudflare host under workerd (the
// resolver Worker behind a service binding, through Miniflare).
//
//   node tools/ResolverDeterminism/check.mjs <transcript.json> [node|workerd]...
//
// The transcript is what Program.cs wrote. The hosts come from multiplayer/packages/resolver, so
// `pnpm install`, then `pnpm --filter @chaos-overlords/resolver bundle` and `build` in multiplayer/
// come first. With no runtime named, both run. Each also takes a snapshot of its own, at the
// transcript's snapshot step, and hands it to Program.cs (`--read-archive`), which reads it as a
// client does. Exits non-zero when any hash differs or the client cannot read a snapshot.
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const [transcriptPath, ...requested] = process.argv.slice(2)
if (!transcriptPath) {
  console.error('usage: node check.mjs <transcript.json> [node|workerd]...')
  process.exit(2)
}
const runtimes = requested.length > 0 ? requested : ['node', 'workerd']
const transcript = JSON.parse(fs.readFileSync(transcriptPath, 'utf8'))
const here = path.dirname(fileURLToPath(import.meta.url))
const repository = path.resolve(here, '..', '..')
const resolverPackage = path.join(repository, 'multiplayer', 'packages', 'resolver')
const load = (relative) => import(pathToFileURL(path.join(resolverPackage, relative)).href)

const { replayTranscript } = await load('test/harness/replay.mjs')

async function onNode() {
  const { startNodeMatchResolver } = await load('dist/node/index.js')
  const started = performance.now()
  const resolver = await startNodeMatchResolver()
  const bootMs = Math.round(performance.now() - started)
  try {
    const result = await replayTranscript(resolver, transcript, 'check-')
    const snapshot = result.ownSnapshot ?? (await snapshotAtStep(resolver))
    return { ...result, bootMs, memoryBytes: (await resolver.info()).memoryBytes, snapshot }
  } finally {
    await resolver.close()
  }
}

async function onWorkerd() {
  const { startWorkerdResolver } = await load('test/harness/workerd.mjs')
  const started = performance.now()
  const { resolver, dispose } = await startWorkerdResolver()
  try {
    await resolver.describe()
    const bootMs = Math.round(performance.now() - started)
    const result = await replayTranscript(resolver, transcript, 'check-')
    const snapshot = result.ownSnapshot ?? (await snapshotAtStep(resolver))
    return { ...result, bootMs, memoryBytes: (await resolver.info('check-own')).memoryBytes, snapshot }
  } finally {
    await dispose()
  }
}

/** The host's own snapshot at the end of a transcript that has no snapshot step. */
async function snapshotAtStep(resolver) {
  const id = 'check-own'
  await resolver.bootstrap(id, {
    seed: transcript.seed,
    gameSettings: transcript.gameSettings,
    players: transcript.players,
  })
  for (const step of transcript.steps) {
    if (step.kind === 'snapshot') break
    await resolver.applyEvent(id, step.event, step.sealedOrders)
  }
  return resolver.snapshot(id)
}

/** Whether the native client reads the host's snapshot to the hash it was taken at. */
function clientReads(snapshot) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'rechaos-archive-'))
  const file = path.join(directory, 'archive.txt')
  fs.writeFileSync(file, snapshot.body)
  try {
    execFileSync(
      process.env.DOTNET || 'dotnet',
      ['run', '--project', here, '-c', 'Release', '--', '--read-archive', file, snapshot.stateHash],
      { stdio: ['ignore', 'ignore', 'inherit'] },
    )
    return true
  } catch {
    return false
  } finally {
    fs.rmSync(directory, { recursive: true, force: true })
  }
}

let failed = false
for (const name of runtimes) {
  const run = name === 'node' ? onNode : name === 'workerd' ? onWorkerd : null
  if (!run) throw new Error(`unknown runtime ${name}`)
  const started = performance.now()
  const result = await run()
  const wallMs = Math.round(performance.now() - started)
  const memoryMb = (result.memoryBytes / 1048576).toFixed(0)
  const read = clientReads(result.snapshot)
  console.log(
    `${name}: ${result.checks} checks, ${result.mismatches.length} mismatches; ` +
      `boot ${result.bootMs} ms, resolving ${result.resolveMs} ms, wall ${wallMs} ms, ` +
      `wasm memory ${memoryMb} MiB; the native client ${read ? 'reads' : 'CANNOT read'} its snapshot`,
  )
  for (const mismatch of result.mismatches) {
    console.log(`  ${mismatch.what}: expected ${mismatch.expected}, got ${mismatch.actual}`)
  }
  failed ||= result.mismatches.length > 0 || !read
}
process.exit(failed ? 1 : 0)

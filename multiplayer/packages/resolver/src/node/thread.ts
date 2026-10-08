/**
 * The worker thread the Node host runs the resolver in. A turn is hundreds of milliseconds of CPU,
 * which on the main thread would stall the event loop and every event stream on it.
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
import { parentPort, workerData } from 'node:worker_threads'
import { type BundleManifest, bootResolver } from '../boot.js'
import { ResolverCore, type ResolverLimits } from '../core.js'
import { encodeResolverError } from '../errors.js'
import type { ThreadReply, ThreadRequest } from './protocol.js'

const { bundleDir, limits } = workerData as { bundleDir: string; limits: ResolverLimits }
if (!parentPort) throw new Error('resolver: thread.js runs only as a worker thread')
const port = parentPort

async function start(): Promise<ResolverCore> {
  const file = (name: string) => join(bundleDir, name)
  const manifest = JSON.parse(readFileSync(file('manifest.json'), 'utf8')) as BundleManifest
  const load = (name: string) =>
    import(pathToFileURL(file(name)).href) as Promise<Record<string, unknown>>
  const [loader, nativeModule, runtimeModule] = await Promise.all([
    load('dotnet.js'),
    load('dotnet.native.js'),
    load('dotnet.runtime.js'),
  ])
  const wasm = await WebAssembly.compile(readFileSync(file('dotnet.native.wasm')))
  const files = new Map(manifest.assemblies.map((asset) => [asset.name, asset.file]))
  const booted = await bootResolver({
    dotnet: loader.dotnet,
    nativeModule,
    runtimeModule,
    wasm,
    manifest,
    assembly: (name) => {
      const path = files.get(name)
      return path ? readFileSync(file(path)) : undefined
    },
  })
  return new ResolverCore(booted, limits)
}

function dispatch(core: ResolverCore, request: ThreadRequest): unknown {
  const [a, b, c] = request.args as [never, never, never]
  switch (request.method) {
    case 'describe': {
      const { sessionVersion, snapshotFormatVersion } = core.info()
      return { sessionVersion, snapshotFormatVersion }
    }
    case 'info':
      return core.info()
    case 'bootstrap':
      return core.bootstrap(a, b)
    case 'restore':
      return core.restore(a, b, c)
    case 'applySealedTurn':
      return core.applySealedTurn(a, b)
    case 'handOverSeat':
      return core.handOverSeat(a, b, c)
    case 'status':
      return core.status(a)
    case 'savePayload':
      return core.savePayload(a)
    case 'release':
      return core.release(a)
    case 'heldMatches':
      return core.heldMatches()
  }
}

start().then(
  (core) => {
    port.on('message', (request: ThreadRequest) => {
      let reply: ThreadReply
      try {
        reply = { id: request.id, ok: true, value: dispatch(core, request) }
      } catch (error: unknown) {
        reply = { id: request.id, ok: false, error: encodeResolverError(error) }
      }
      const payload = (reply.ok && (reply.value as { payload?: unknown })?.payload) as
        | Uint8Array
        | undefined
      port.postMessage(reply, payload instanceof Uint8Array ? [payload.buffer as ArrayBuffer] : [])
    })
    port.postMessage({ ready: true })
  },
  (error: unknown) =>
    port.postMessage({ ready: false, error: String((error as Error)?.stack ?? error) }),
)

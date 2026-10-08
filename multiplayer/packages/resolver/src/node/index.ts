import { existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { Worker } from 'node:worker_threads'
import { nodeBrotliCodec } from '../codec-node.js'
import type {
  BootstrapInput,
  MatchStatus,
  ResolverInfo,
  ResolverLimits,
  RestoreInput,
} from '../core.js'
import { decodeResolverError } from '../errors.js'
import {
  type MatchResolver,
  type PayloadResolver,
  type ResolverDescription,
  withArchives,
} from '../resolver.js'
import type { ThreadMethod, ThreadReply, ThreadRequest, ThreadStartup } from './protocol.js'

/**
 * The defaults for a Node process. The runtime starts at about 4 MiB of managed heap, a match of
 * 26 turns adds about 1.5 MiB and one of 104 turns about 8 MiB (measured for #511), and the .NET
 * runtime for WebAssembly may grow its memory to 2 GiB, so these leave a self-hosted server a wide
 * margin.
 */
export const NODE_RESOLVER_DEFAULTS: ResolverLimits = {
  maxMatches: 64,
  managedHeapBudgetBytes: 512 * 1024 * 1024,
}

export interface NodeResolverOptions extends Partial<ResolverLimits> {
  /** The bundle directory; the one this package ships by default. */
  bundleDir?: string
}

/** The Node host: a {@link PayloadResolver} running in a worker thread. */
export interface NodeResolverHost extends PayloadResolver {
  /** Memory and holdings of the thread's runtime, starting it if it is not running. */
  info(): Promise<ResolverInfo>
  /** The ids held, least recently used first. */
  heldMatches(): Promise<string[]>
  /** Stops the thread. Every match goes with it. */
  close(): Promise<void>
}

/** The bundle this package ships, next to `dist/`. */
export function defaultBundleDir(): string {
  return fileURLToPath(new URL('../../bundle/', import.meta.url))
}

/**
 * Starts the resolver in a worker thread and waits until it has booted.
 *
 * A thread that dies (a crash, the runtime running out of memory) rejects what was waiting on it,
 * and the next call starts a new one, which holds nothing: every match answers `MatchNotHeldError`
 * until its caller rebuilds it.
 */
export async function startNodeResolverHost(
  options: NodeResolverOptions = {},
): Promise<NodeResolverHost> {
  const bundleDir = options.bundleDir ?? defaultBundleDir()
  if (!existsSync(bundleDir)) {
    throw new Error(
      `resolver: no bundle at ${bundleDir}; run \`pnpm --filter @chaos-overlords/resolver bundle\``,
    )
  }
  const limits: ResolverLimits = {
    maxMatches: options.maxMatches ?? NODE_RESOLVER_DEFAULTS.maxMatches,
    managedHeapBudgetBytes:
      options.managedHeapBudgetBytes ?? NODE_RESOLVER_DEFAULTS.managedHeapBudgetBytes,
  }
  const threadUrl = new URL('./thread.js', import.meta.url)

  let thread: Promise<Worker> | undefined
  let closed = false
  let nextId = 1
  const pending = new Map<
    number,
    { resolve(value: unknown): void; reject(error: Error): void; matchId: string }
  >()

  const failAll = (error: Error) => {
    for (const waiter of pending.values()) waiter.reject(error)
    pending.clear()
  }

  const start = (): Promise<Worker> => {
    const started = new Promise<Worker>((resolve, reject) => {
      const worker = new Worker(threadUrl, { workerData: { bundleDir, limits } })
      let ready = false
      let gone = false
      worker.on('message', (message: ThreadStartup | ThreadReply) => {
        if ('ready' in message) {
          if (message.ready) {
            ready = true
            resolve(worker)
          } else {
            reject(new Error(`resolver: the runtime did not start: ${message.error}`))
            void worker.terminate()
          }
          return
        }
        const waiter = pending.get(message.id)
        if (!waiter) return
        pending.delete(message.id)
        if (message.ok) waiter.resolve(message.value)
        else waiter.reject(decodeResolverError(message.error, waiter.matchId))
      })
      // A thread that fails emits 'error' and then 'exit'. Only the first counts, and only while the
      // thread is still the current one: by the 'exit', a caller may have started its replacement,
      // whose reference and waiters this thread must not take with it.
      const lost = (reason: string) => {
        if (gone) return
        gone = true
        if (!ready) reject(new Error(`resolver: the runtime did not start: ${reason}`))
        if (thread !== started) return
        thread = undefined
        failAll(new Error(`resolver: the runtime thread stopped: ${reason}`))
      }
      worker.on('error', (error: Error) => lost(String(error.stack ?? error)))
      worker.on('exit', (code) => lost(`exit code ${code}`))
    })
    return started
  }

  const call = async <T>(method: ThreadMethod, args: unknown[], matchId = ''): Promise<T> => {
    if (closed) throw new Error('resolver: the host is closed')
    thread ??= start()
    const worker = await thread
    const id = nextId++
    return new Promise<T>((resolve, reject) => {
      pending.set(id, { resolve: resolve as (value: unknown) => void, reject, matchId })
      const request: ThreadRequest = { id, method, args }
      // A worker thread's port, which has no target origin; the rule is about window.postMessage.
      // oxlint-disable-next-line unicorn/require-post-message-target-origin
      worker.postMessage(request)
    })
  }

  await (thread = start())

  return {
    describe: () => call<ResolverDescription>('describe', []),
    info: () => call<ResolverInfo>('info', []),
    heldMatches: () => call<string[]>('heldMatches', []),
    bootstrap: (matchId: string, input: BootstrapInput) =>
      call<MatchStatus>('bootstrap', [matchId, input], matchId),
    restore: (matchId: string, savePayload: Uint8Array, stateHash: string, input: RestoreInput) =>
      call<MatchStatus>('restore', [matchId, savePayload, stateHash, input], matchId),
    applyEvent: (matchId: string, event: unknown, sealedOrders?: unknown) =>
      call<MatchStatus>('applyEvent', [matchId, event, sealedOrders ?? null], matchId),
    status: (matchId: string) => call<MatchStatus | null>('status', [matchId], matchId),
    savePayload: (matchId: string) =>
      call<{ payload: Uint8Array; status: MatchStatus }>('savePayload', [matchId], matchId),
    release: (matchId: string) => call<void>('release', [matchId], matchId),
    close: async () => {
      closed = true
      const running = thread
      thread = undefined
      failAll(new Error('resolver: the host is closed'))
      if (running) await (await running).terminate()
    },
  }
}

/** The Node host with snapshots in the archive form clients upload, compressed with `node:zlib`. */
export async function startNodeMatchResolver(
  options: NodeResolverOptions = {},
): Promise<MatchResolver & { close(): Promise<void>; info(): Promise<ResolverInfo> }> {
  const host = await startNodeResolverHost(options)
  return {
    ...withArchives(host, nodeBrotliCodec),
    close: () => host.close(),
    info: () => host.info(),
  }
}

/**
 * The Cloudflare host: a Worker of its own, which the coordination Worker reaches over a service
 * binding, with a Durable Object per match so that every call for a match lands where the match is
 * held.
 *
 * It runs without the `nodejs_compat` flag. Under that flag the .NET loader takes workerd for Node
 * and reaches for `node:` modules a Worker does not have in the form it expects. The archive around
 * a snapshot is therefore written and read on the coordination Worker's side (see
 * `../cloudflare/index.ts`), which has `node:zlib`, and this Worker exchanges bare save payloads.
 *
 * `worker/index.js` imports the bundle's modules statically and passes them to
 * {@link useResolverBundle}, because a Worker can import nothing at run time.
 */
import { DurableObject, WorkerEntrypoint } from 'cloudflare:workers'
import type { DurableObjectNamespace } from '@cloudflare/workers-types'
import { type BundleManifest, bootResolver } from '../boot.js'
import {
  type BootstrapInput,
  type FeedResult,
  type FeedStep,
  type MatchStatus,
  ResolverCore,
  type ResolverInfo,
  type ResolverLimits,
  type RestoreInput,
} from '../core.js'
import { encodeResolverError } from '../errors.js'
import type { ResolverDescription } from '../resolver.js'
import { WORKERD_RESOLVER_DEFAULTS } from './limits.js'

export { WORKERD_RESOLVER_DEFAULTS }

/** The bindings and vars of the resolver Worker. */
export interface ResolverWorkerEnv {
  /** The Durable Object namespace of {@link MatchResolverObject}. */
  MATCH_RESOLVER: DurableObjectNamespace
  /** Overrides {@link WORKERD_RESOLVER_DEFAULTS}.maxMatches. */
  RESOLVER_MAX_MATCHES?: string
  /** Overrides {@link WORKERD_RESOLVER_DEFAULTS}.managedHeapBudgetBytes, in MiB. */
  RESOLVER_MANAGED_HEAP_MIB?: string
}

/** The bundle's modules, as `worker/index.js` imports them. */
export interface WorkerBundle {
  dotnet: unknown
  nativeModule: unknown
  runtimeModule: unknown
  wasm: WebAssembly.Module
  manifest: BundleManifest
  assemblies: Record<string, ArrayBuffer>
}

/** What the coordination Worker calls over the service binding. Every method is an RPC. */
export interface ResolverService {
  describe(): Promise<ResolverDescription>
  bootstrap(matchId: string, input: BootstrapInput): Promise<MatchStatus>
  restore(
    matchId: string,
    savePayload: Uint8Array,
    stateHash: string,
    input: RestoreInput,
  ): Promise<MatchStatus>
  applyEvent(matchId: string, event: unknown, sealedOrders: unknown): Promise<MatchStatus>
  applyEvents(matchId: string, fromTurn: number, steps: readonly FeedStep[]): Promise<FeedResult>
  status(matchId: string): Promise<MatchStatus | null>
  savePayload(matchId: string): Promise<{ payload: Uint8Array; status: MatchStatus }>
  seatViewPayload(
    matchId: string,
    slot: number,
  ): Promise<{ payload: Uint8Array | null; status: MatchStatus }>
  release(matchId: string): Promise<void>
  /** The memory and holdings of the isolate the match's object runs in, for tests and operators. */
  info(matchId: string): Promise<ResolverIsolateInfo>
}

export interface ResolverIsolateInfo extends ResolverInfo {
  heldMatches: string[]
}

function limitsFrom(env: ResolverWorkerEnv): ResolverLimits {
  const whole = (raw: string | undefined, fallback: number) => {
    const value = Number(raw)
    return raw !== undefined && raw !== '' && Number.isInteger(value) && value > 0
      ? value
      : fallback
  }
  return {
    maxMatches: whole(env.RESOLVER_MAX_MATCHES, WORKERD_RESOLVER_DEFAULTS.maxMatches),
    managedHeapBudgetBytes:
      whole(
        env.RESOLVER_MANAGED_HEAP_MIB,
        WORKERD_RESOLVER_DEFAULTS.managedHeapBudgetBytes / 1048576,
      ) * 1048576,
  }
}

let bundle: WorkerBundle | undefined
// One runtime per isolate, shared by every object the isolate runs. A boot that fails is retried by
// the next call rather than remembered.
let core: Promise<ResolverCore> | undefined

/** Hands the Worker the bundle's modules; `worker/index.js` calls it before anything runs. */
export function useResolverBundle(modules: WorkerBundle): void {
  bundle = modules
  core = undefined
}

function loaded(): WorkerBundle {
  if (!bundle) throw new Error('resolver: the Worker was started without its bundle')
  return bundle
}

function coreFor(env: ResolverWorkerEnv): Promise<ResolverCore> {
  const modules = loaded()
  core ??= bootResolver({
    dotnet: modules.dotnet,
    nativeModule: modules.nativeModule,
    runtimeModule: modules.runtimeModule,
    wasm: modules.wasm,
    manifest: modules.manifest,
    assembly: (name) => modules.assemblies[name],
  }).then(
    (booted) => new ResolverCore(booted, limitsFrom(env)),
    (error: unknown) => {
      core = undefined
      throw error
    },
  )
  return core
}

/** Runs a call on the isolate's runtime, with the error code in the message it crosses RPC with. */
async function run<T>(env: ResolverWorkerEnv, call: (core: ResolverCore) => T): Promise<T> {
  const resolver = await coreFor(env)
  try {
    return call(resolver)
  } catch (error: unknown) {
    throw new Error(encodeResolverError(error), { cause: error })
  }
}

/** One per match: every call for a match is routed to the isolate its object runs in. */
export class MatchResolverObject extends DurableObject<ResolverWorkerEnv> {
  bootstrap(matchId: string, input: BootstrapInput): Promise<MatchStatus> {
    return run(this.env, (resolver) => resolver.bootstrap(matchId, input))
  }
  restore(
    matchId: string,
    savePayload: Uint8Array,
    stateHash: string,
    input: RestoreInput,
  ): Promise<MatchStatus> {
    return run(this.env, (resolver) => resolver.restore(matchId, savePayload, stateHash, input))
  }
  applyEvent(matchId: string, event: unknown, sealedOrders: unknown): Promise<MatchStatus> {
    return run(this.env, (resolver) => resolver.applyEvent(matchId, event, sealedOrders))
  }
  applyEvents(matchId: string, fromTurn: number, steps: readonly FeedStep[]): Promise<FeedResult> {
    return run(this.env, (resolver) => resolver.applyEvents(matchId, fromTurn, steps))
  }
  status(matchId: string): Promise<MatchStatus | null> {
    return run(this.env, (resolver) => resolver.status(matchId))
  }
  savePayload(matchId: string): Promise<{ payload: Uint8Array; status: MatchStatus }> {
    return run(this.env, (resolver) => resolver.savePayload(matchId))
  }
  seatViewPayload(
    matchId: string,
    slot: number,
  ): Promise<{ payload: Uint8Array | null; status: MatchStatus }> {
    return run(this.env, (resolver) => resolver.seatViewPayload(matchId, slot))
  }
  release(matchId: string): Promise<void> {
    return run(this.env, (resolver) => resolver.release(matchId))
  }
  info(): Promise<ResolverIsolateInfo> {
    return run(this.env, (resolver) => ({
      ...resolver.info(),
      heldMatches: resolver.heldMatches(),
    }))
  }
}

function objectFor(env: ResolverWorkerEnv, matchId: string): MatchResolverObject {
  const namespace = env.MATCH_RESOLVER
  return namespace.get(namespace.idFromName(matchId)) as unknown as MatchResolverObject
}

/** The service binding's surface: each call goes to the match's object. */
export class ResolverEntrypoint
  extends WorkerEntrypoint<ResolverWorkerEnv>
  implements ResolverService
{
  async describe(): Promise<ResolverDescription> {
    const { manifest } = loaded()
    return {
      sessionVersion: manifest.sessionVersion,
      snapshotFormatVersion: manifest.snapshotFormatVersion,
    }
  }
  bootstrap(matchId: string, input: BootstrapInput): Promise<MatchStatus> {
    return objectFor(this.env, matchId).bootstrap(matchId, input)
  }
  restore(
    matchId: string,
    savePayload: Uint8Array,
    stateHash: string,
    input: RestoreInput,
  ): Promise<MatchStatus> {
    return objectFor(this.env, matchId).restore(matchId, savePayload, stateHash, input)
  }
  applyEvent(matchId: string, event: unknown, sealedOrders: unknown): Promise<MatchStatus> {
    return objectFor(this.env, matchId).applyEvent(matchId, event, sealedOrders)
  }
  applyEvents(matchId: string, fromTurn: number, steps: readonly FeedStep[]): Promise<FeedResult> {
    return objectFor(this.env, matchId).applyEvents(matchId, fromTurn, steps)
  }
  status(matchId: string): Promise<MatchStatus | null> {
    return objectFor(this.env, matchId).status(matchId)
  }
  savePayload(matchId: string): Promise<{ payload: Uint8Array; status: MatchStatus }> {
    return objectFor(this.env, matchId).savePayload(matchId)
  }
  seatViewPayload(
    matchId: string,
    slot: number,
  ): Promise<{ payload: Uint8Array | null; status: MatchStatus }> {
    return objectFor(this.env, matchId).seatViewPayload(matchId, slot)
  }
  release(matchId: string): Promise<void> {
    return objectFor(this.env, matchId).release(matchId)
  }
  info(matchId: string): Promise<ResolverIsolateInfo> {
    return objectFor(this.env, matchId).info()
  }
}

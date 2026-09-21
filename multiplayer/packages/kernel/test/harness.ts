import type { OrderDocument } from '@chaos-overlords/contracts'
import { createKernel, type Kernel, type Principal } from '../src'
import {
  type CountingStorage,
  InMemoryStorage,
  ManualClock,
  RecordingLogger,
  RecordingNotifier,
  RecordingScheduler,
  RecordingStreamCloser,
} from '../src/testing'

/**
 * A one-op order document for a principal, distinguishable by `gang`. Ops carry the submitter's own
 * slot, because the server refuses any that name another player.
 */
export const ordersFor = (principal: Principal, gang: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'cancelCommand', player: principal.player.slot, gang }],
})

export const gangOf = (document: OrderDocument): number | undefined => {
  const op = document.ops[0]
  return op && 'gang' in op ? op.gang : undefined
}

export const HASH_A = 'a'.repeat(32)
export const HASH_B = 'b'.repeat(32)

/**
 * A kernel over the in-memory reference storage, with every runtime port recording what it was
 * asked to do.
 *
 * One object rather than a beforeEach that assigns half a dozen `let`s, so a suite can be split
 * across files without each half restating the wiring.
 */
export interface Harness {
  kernel: Kernel
  storage: InMemoryStorage
  clock: ManualClock
  notifier: RecordingNotifier
  scheduler: RecordingScheduler
  streams: RecordingStreamCloser
  logger: RecordingLogger
  /** Submit a one-op document for a principal, in the vocabulary the server accepts. */
  submit(principal: Principal, turn: number, gang: number, ready: boolean): Promise<unknown>
  principalOf(token: string): Promise<Principal>
  startedMatch(turnTimerSeconds?: number): Promise<{
    host: Awaited<ReturnType<Kernel['lobby']['createMatch']>>
    guest: Awaited<ReturnType<Kernel['lobby']['join']>>
  }>
  /** A started match of three, the smallest roster where a majority can outvote the host. */
  startedMatchOfThree(turnTimerSeconds?: number): Promise<{
    host: Awaited<ReturnType<Kernel['lobby']['createMatch']>>
    guest: Awaited<ReturnType<Kernel['lobby']['join']>>
    third: Awaited<ReturnType<Kernel['lobby']['join']>>
  }>
}

export function createHarness(): Harness {
  const storage = new InMemoryStorage()
  const clock = new ManualClock()
  const notifier = new RecordingNotifier()
  const scheduler = new RecordingScheduler()
  const streams = new RecordingStreamCloser()
  const logger = new RecordingLogger()
  const kernel = createKernel({ storage, notifier, scheduler, streams, clock, logger })

  const principalOf = (token: string) => kernel.auth.authenticate(token)

  return {
    kernel,
    storage,
    clock,
    notifier,
    scheduler,
    streams,
    logger,
    principalOf,
    submit: (principal, turn, gang, ready) =>
      kernel.turns.submitOrders(principal, turn, { orders: ordersFor(principal, gang), ready }),
    startedMatch: async (turnTimerSeconds = 0) => {
      const host = await kernel.lobby.createMatch({
        settings: {
          name: 'Night City',
          maxPlayers: 3,
          turnTimerSeconds,
          visibility: 'public',
          gameSettings: { scenario: 3 },
        },
        hostDisplayName: 'Host',
      })
      const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
      await kernel.lobby.start(await principalOf(host.token))
      return { host, guest }
    },
    startedMatchOfThree: async (turnTimerSeconds = 0) => {
      const host = await kernel.lobby.createMatch({
        settings: {
          name: 'Night City',
          maxPlayers: 3,
          turnTimerSeconds,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'Host',
      })
      const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
      const third = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Third' })
      await kernel.lobby.start(await principalOf(host.token))
      return { host, guest, third }
    },
  }
}

/**
 * The same kernel over a counting wrapper of the harness's own storage.
 *
 * Every other port stays the harness's, so a cost assertion is made against the state the rest of
 * the test set up rather than against a fresh world.
 */
export function countedKernel(h: Harness, counting: CountingStorage): Kernel {
  return createKernel({
    storage: counting.storage,
    notifier: h.notifier,
    scheduler: h.scheduler,
    streams: h.streams,
    clock: h.clock,
    logger: h.logger,
  })
}

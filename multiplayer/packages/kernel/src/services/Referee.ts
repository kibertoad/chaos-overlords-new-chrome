import { MULTIPLAYER_PROTOCOL_VERSION } from '@chaos-overlords/contracts'
import type { Match, Player, Turn } from '../domain/entities'
import {
  isMatchNotHeld,
  type ResolverFeedStep,
  type ResolverMatchStatus,
  type ResolverSnapshot,
} from '../ports/resolver'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'
import { requireParticipant } from './guards'
import { type MatchQueryService, toPlayerView } from './MatchQueryService'

/** Who a snapshot the server wrote itself names as its uploader. */
export const SERVER_SNAPSHOT_UPLOADER = 'server'

/** Turns between two of the server's checkpoints, as between two of the host's. */
export const SERVER_CHECKPOINT_EVERY_TURNS = 10

/**
 * Snapshots kept per live match. Enough to cover a desync being repaired while an earlier one is
 * still being fetched by a straggler, and far fewer than a long match would otherwise accumulate.
 */
export const SNAPSHOTS_KEPT_PER_MATCH = 5

/**
 * The most seals one feed carries. A seal costs the resolver about half a second, and on
 * Cloudflare each feed is one call to the match's Durable Object, which has a CPU limit of its own.
 */
const SEALS_PER_FEED = 10

const EVENT_PAGE = 200

/** The events that change the resolver's state; every other one it would read and pass over. */
const FED_EVENT_TYPES: ReadonlySet<string> = new Set([
  'match.playerTakenOver',
  'match.playerReturned',
  'match.latePlayerJoined',
  'turn.opened',
  'turn.sealed',
])

/** What {@link Referee.resolveThrough} achieved. */
export interface ResolveOutcome {
  /**
   * `resolved`: the turn asked about has a resolution now. `pending`: another caller is feeding
   * the same match, or the log does not carry the seal yet, and that caller or a later one records
   * it. `unavailable`: the server does not referee this match, or its resolver failed.
   */
  kind: 'resolved' | 'pending' | 'unavailable'
  /** The turns whose resolution this call recorded, oldest first. Each is owed its verdict. */
  recorded: number[]
}

/** Where a held match stands and the sequence number of the last event it was fed. */
interface Position {
  status: ResolverMatchStatus
  cursor: number
}

/**
 * The server's side of refereeing: keeps the resolver's copy of each refereed match level with the
 * event log, records what each sealed turn resolved to, and writes the snapshots clients adopt.
 *
 * Nothing here is held between calls. Where a held match stands is the resolver's answer, and what
 * it was last fed is the `resolvedSeq` recorded on the turn before it, so one process, many
 * Workers or a restarted server all pick up from the same facts. A feed is applied only if the
 * match is still on the turn it was written for (see `TurnResolver.applyEvents`), so two callers
 * racing to feed the same events apply them once. A match the resolver does not hold, or holds at a
 * state the turn rows do not explain, is rebuilt from the newest snapshot whose hash a resolution
 * recorded, or from `match.started`, and fed the log after it.
 */
export class Referee {
  private description:
    | Promise<{ sessionVersion: number; snapshotFormatVersion: number } | null>
    | undefined

  constructor(
    private readonly deps: KernelDeps,
    private readonly query: MatchQueryService,
    private readonly publisher: EventPublisher,
  ) {}

  /** Whether the server resolves this match's turns itself. */
  async referees(match: Match): Promise<boolean> {
    if (!this.deps.resolver || match.seed === null) return false
    const description = await this.describe()
    return description?.sessionVersion === match.sessionVersion
  }

  /**
   * Bring the resolver's copy of the match up to turn `through`, recording the resolution of
   * every seal on the way. Never throws: a resolver that fails leaves the turns it did not reach
   * to the reports.
   */
  async resolveThrough(match: Match, through: number): Promise<ResolveOutcome> {
    const recorded: number[] = []
    if (!(await this.referees(match))) return { kind: 'unavailable', recorded }
    try {
      return { kind: await this.feed(match, match.id, through, recorded), recorded }
    } catch (error) {
      this.deps.logger.warn('the server could not resolve a turn; the reports decide it', {
        matchId: match.id,
        turn: through,
        error: String(error),
      })
      return { kind: 'unavailable', recorded }
    }
  }

  /**
   * Resolve the match through turn `through` and run `settle` on every turn that resolution
   * decided. Best effort: a resolver that fails leaves the turns it did not reach to the reports,
   * and the seal or report that called this has already done its own work. Returns before any
   * storage read on a server without a resolver, so seals and reports there cost what they did.
   */
  async settleThrough(
    match: Match,
    through: number,
    settle: (turn: number) => Promise<unknown>,
  ): Promise<ResolveOutcome['kind']> {
    if (!this.deps.resolver) return 'unavailable'
    try {
      const outcome = await this.resolveThrough(match, through)
      for (const turn of outcome.recorded) await settle(turn)
      return outcome.kind
    } catch (error) {
      this.deps.logger.warn('could not settle a turn the server resolved; a later seal or report retries it', {
        matchId: match.id,
        turn: through,
        error: String(error),
      })
      return 'unavailable'
    }
  }

  /**
   * Judge one seat's report of turn `number` when the server has already confirmed that turn on
   * its own state, and answer whether it had. A refereed match finishes when the server resolves
   * its last turn, before anyone has reported it, so the reports a finished match receives are
   * judged here too.
   */
  async judgeDecided(
    match: Match,
    player: Player,
    number: number,
    stateHash: string,
  ): Promise<boolean> {
    if (!this.deps.resolver || match.status === 'lobby' || match.status === 'abandoned')
      return false
    const turn = await this.deps.storage.turns.get(match.id, number)
    // A turn the reports confirmed while the resolver was down keeps the state they agreed on, and
    // a resolution recorded for it afterwards does not overrule what `turn.confirmed` announced.
    if (
      turn?.status !== 'confirmed' ||
      turn.resolvedHash === null ||
      turn.resolvedHash !== turn.stateHash
    ) {
      return false
    }
    requireParticipant(player)
    await this.judge(match, turn, player.id, stateHash)
    return true
  }

  /**
   * Confirm a turn on the state the server resolved it to, with `finish` doing what follows any
   * confirmation. The reports play no part in it: each one on file is judged against the state
   * afterwards, and a seat whose report differs is told it diverged. A turn the reports had
   * already paused the match over (the resolver was not reachable then) is confirmed the same
   * way, which lifts the pause.
   */
  async confirm(
    match: Match,
    turn: Turn,
    finish: (verdict: { stateHash: string; finished: boolean }) => Promise<unknown>,
  ): Promise<boolean> {
    const stateHash = turn.resolvedHash
    if (stateHash === null) return false
    const won = await this.deps.storage.turns.transition(
      match.id,
      turn.number,
      ['sealed', 'desynced'],
      { status: 'confirmed', stateHash },
    )
    if (!won) return false
    this.deps.logger.info('turn confirmed on the server state', {
      matchId: match.id,
      turn: turn.number,
    })
    await finish({ stateHash, finished: turn.resolvedFinished === true })
    await this.judgeReports(match, { ...turn, status: 'confirmed', stateHash })
    return false
  }

  /** Judge every report on file for a turn the server confirmed on its own state. */
  async judgeReports(match: Match, turn: Turn): Promise<void> {
    for (const report of await this.deps.storage.turns.listReports(match.id, turn.number)) {
      await this.judge(match, turn, report.playerId, report.stateHash)
    }
  }

  /**
   * Check one seat's report against the state the server resolved the turn to. A report that
   * differs is that seat's divergence alone: the seat is told, with the server's snapshot of the
   * turn stored first so that it can adopt it. Nobody else is paused or asked anything.
   */
  async judge(
    match: Match,
    turn: Turn,
    playerId: string,
    reportedStateHash: string,
  ): Promise<void> {
    const decided = turn.resolvedHash
    if (decided === null || decided === reportedStateHash) return
    const stored = await this.ensureSnapshot(match, turn.number)
    if (stored !== decided) {
      this.deps.logger.warn('a seat diverged and the server has no snapshot to give it', {
        matchId: match.id,
        turn: turn.number,
        playerId,
      })
      return
    }
    const told = await this.publisher.publishOnce(
      match.id,
      divergenceKey(turn.number, playerId, reportedStateHash),
      {
        type: 'turn.diverged',
        payload: { turn: turn.number, playerId, stateHash: decided, reportedStateHash },
      },
    )
    if (told) {
      this.deps.logger.info('a seat diverged from the server', {
        matchId: match.id,
        turn: turn.number,
        playerId,
      })
    }
  }

  /**
   * The hash of the server's own snapshot of the state after `turn` (0: the starting state),
   * writing it first when it is not stored. Null when the server does not referee the match or
   * cannot produce the snapshot.
   *
   * A seat whose report differs is told to adopt this snapshot, so it must exist before the seat
   * is told. The resolver usually holds the match right after the turn, because reports arrive
   * while the next turn is being planned; otherwise the state is built off to the side, under an
   * id of its own, and released.
   */
  async ensureSnapshot(match: Match, turn: number): Promise<string | null> {
    const resolver = this.deps.resolver
    if (!resolver || !(await this.referees(match))) return null
    const id = match.id
    try {
      const expected =
        turn === 0 ? null : ((await this.deps.storage.turns.get(id, turn))?.resolvedHash ?? null)
      if (turn > 0 && expected === null) return null
      const existing = await this.deps.storage.snapshots.getSummary(id, turn)
      if (
        existing &&
        (turn === 0
          ? existing.uploadedByPlayerId === SERVER_SNAPSHOT_UPLOADER
          : existing.stateHash === expected)
      ) {
        return existing.stateHash
      }
      const held = await resolver.status(id)
      if (held?.turn === turn + 1 && (expected === null || held.stateHash === expected)) {
        const snapshot = await resolver.snapshot(id)
        if (snapshot.stateHash === held.stateHash) {
          await this.store(match, turn, snapshot)
          return snapshot.stateHash
        }
      }
      // One id per call: two reports judged at once would otherwise build under the same id, and
      // the first to finish would release the copy the other is still reading.
      const scratch = `${id}~${turn}~${crypto.randomUUID()}`
      try {
        await this.feed(match, scratch, turn, null)
        const snapshot = await resolver.snapshot(scratch)
        if (
          snapshot.status.turn !== turn + 1 ||
          (expected !== null && snapshot.stateHash !== expected)
        ) {
          this.deps.logger.error('the server rebuilt a turn to a state it did not record', {
            matchId: id,
            turn,
            recorded: expected,
            rebuilt: snapshot.stateHash,
          })
          return null
        }
        await this.store(match, turn, snapshot)
        return snapshot.stateHash
      } finally {
        await resolver.release(scratch).catch(() => undefined)
      }
    } catch (error) {
      this.deps.logger.warn('the server could not write its snapshot of a turn', {
        matchId: id,
        turn,
        error: String(error),
      })
      return null
    }
  }

  private describe() {
    const resolver = this.deps.resolver
    if (!resolver) return Promise.resolve(null)
    this.description ??= resolver.describe().catch((error: unknown) => {
      // Asked again on the next call: a resolver that is down now may not be later.
      this.description = undefined
      this.deps.logger.warn('the turn resolver could not be reached', { error: String(error) })
      return null
    })
    return this.description
  }

  /**
   * Feed the match held under `id` until it has applied the seal of turn `through`. With
   * `recorded`, each seal's resolution is written to its turn and checkpoints are taken; without
   * it (a copy built off to the side) nothing is written.
   */
  private async feed(
    match: Match,
    id: string,
    through: number,
    recorded: number[] | null,
  ): Promise<'resolved' | 'pending'> {
    const resolver = this.deps.resolver
    if (!resolver) throw new Error('no turn resolver')
    let position = (await this.position(match.id, id)) ?? (await this.rebuild(match, id, through))
    let rebuilt = false
    for (;;) {
      const { status, cursor } = position
      if (status.turn > through || status.finished) return 'resolved'
      const batch = await this.nextBatch(match, cursor, status.turn, through)
      if (batch.sealSeqs.size === 0) return 'pending'
      let result
      try {
        result = await resolver.applyEvents(id, status.turn, batch.steps)
      } catch (error) {
        // Released between the read of where it stood and the feed: rebuilt once, then fed again.
        if (!isMatchNotHeld(error) || rebuilt) throw error
        rebuilt = true
        position = await this.rebuild(match, id, through)
        continue
      }
      if (!result.applied) return 'pending'
      if (recorded) {
        for (const seal of result.seals) {
          const resolvedSeq = batch.sealSeqs.get(seal.turn)
          if (resolvedSeq === undefined) continue
          if (await this.record(id, seal, resolvedSeq)) recorded.push(seal.turn)
        }
        const last = result.seals.at(-1)
        if (last && (last.turn % SERVER_CHECKPOINT_EVERY_TURNS === 0 || last.finished)) {
          await this.checkpoint(match, last.turn, last.stateHash)
        }
      }
      position = { status: result.status, cursor: batch.lastSeq }
    }
  }

  /** Writes one seal's resolution; false when one was already recorded. */
  private async record(
    matchId: string,
    seal: { turn: number; stateHash: string; finished: boolean },
    resolvedSeq: number,
  ): Promise<boolean> {
    const written = await this.deps.storage.turns.recordResolution(matchId, seal.turn, {
      resolvedHash: seal.stateHash,
      resolvedFinished: seal.finished,
      resolvedSeq,
    })
    if (written) return true
    const standing = (await this.deps.storage.turns.get(matchId, seal.turn))?.resolvedHash
    if (standing !== undefined && standing !== null && standing !== seal.stateHash) {
      // Two resolutions of one sealed set differ only when the rules changed under a session
      // version that did not move. The first one stands, so the match stays consistent.
      this.deps.logger.error(
        'the resolver reached a different state for a turn it resolved before',
        {
          matchId,
          turn: seal.turn,
          recorded: standing,
          reached: seal.stateHash,
        },
      )
    }
    return false
  }

  /**
   * Where the match held under `id` stands and what it was last fed, or null when it is not held
   * or stands somewhere the turn rows do not explain.
   */
  private async position(matchId: string, id: string): Promise<Position | null> {
    const resolver = this.deps.resolver
    if (!resolver) return null
    const status = await resolver.status(id)
    if (!status) return null
    // A finished match takes no more seals, so where its feed stopped no longer matters.
    if (status.finished) return { status, cursor: 0 }
    // Nothing sealed yet: a bootstrap, which has been fed nothing.
    if (status.turn === 1) return { status, cursor: 0 }
    const previous = await this.deps.storage.turns.get(matchId, status.turn - 1)
    if (
      previous === null ||
      previous.resolvedSeq === null ||
      previous.resolvedHash !== status.stateHash
    ) {
      return null
    }
    return { status, cursor: previous.resolvedSeq }
  }

  /**
   * Builds the match under `id` again: from the newest snapshot at or before `through` whose hash
   * a resolution recorded, or from `match.started`.
   */
  private async rebuild(match: Match, id: string, through: number): Promise<Position> {
    const resolver = this.deps.resolver
    if (!resolver || match.seed === null) throw new Error('no turn resolver')
    const checkpoint = await this.trustedCheckpoint(match, through)
    if (checkpoint) {
      const roster = await this.deps.storage.players.listByMatch(match.id)
      const status = await resolver.restore(
        id,
        { body: checkpoint.body, stateHash: checkpoint.stateHash },
        { players: roster.map((player) => toPlayerView(player, match.hostPlayerId)) },
      )
      return { status, cursor: checkpoint.resolvedSeq }
    }
    const started = await this.deps.storage.events.latestOfType(match.id, 'match.started')
    if (started?.type !== 'match.started') throw new Error('the log has no match.started')
    const status = await resolver.bootstrap(id, {
      seed: match.seed,
      gameSettings: match.settings.gameSettings,
      players: started.payload.players,
    })
    return { status, cursor: 0 }
  }

  /**
   * The newest stored snapshot at or before `through` that the server can build on: one whose
   * hash is the resolution recorded for its turn. A snapshot is only ever trusted for being the
   * state the server reached itself, never for who uploaded it, so a host's checkpoint that agrees
   * serves as well as the server's own, and a repair the reports chose does not serve at all.
   */
  private async trustedCheckpoint(
    match: Match,
    through: number,
  ): Promise<(ResolverSnapshot & { resolvedSeq: number }) | null> {
    const { snapshots, turns } = this.deps.storage
    const latest = await snapshots.getLatestSummary(match.id)
    const candidates = new Set<number>()
    if (latest && latest.turn <= through) candidates.add(latest.turn)
    const newestCheckpoint = through - (through % SERVER_CHECKPOINT_EVERY_TURNS)
    for (let i = 0; i < SNAPSHOTS_KEPT_PER_MATCH; i++) {
      candidates.add(newestCheckpoint - i * SERVER_CHECKPOINT_EVERY_TURNS)
    }
    for (const turn of [...candidates].filter((t) => t > 0).sort((a, b) => b - a)) {
      const summary = latest?.turn === turn ? latest : await snapshots.getSummary(match.id, turn)
      if (!summary) continue
      const row = await turns.get(match.id, turn)
      if (row?.resolvedHash !== summary.stateHash || row.resolvedSeq === null) continue
      const snapshot = await snapshots.get(match.id, turn)
      if (snapshot?.stateHash !== summary.stateHash) continue
      return { body: snapshot.body, stateHash: snapshot.stateHash, resolvedSeq: row.resolvedSeq }
    }
    return null
  }

  /**
   * The events after `cursor` up to the seal that ends this feed: the seal of `through`, a
   * checkpoint's seal, or the {@link SEALS_PER_FEED}th. Only events that change the state are
   * carried. Empty when the log holds no seal past `cursor` yet.
   */
  private async nextBatch(
    match: Match,
    cursor: number,
    fromTurn: number,
    through: number,
  ): Promise<{ steps: ResolverFeedStep[]; sealSeqs: Map<number, number>; lastSeq: number }> {
    const steps: ResolverFeedStep[] = []
    const sealSeqs = new Map<number, number>()
    let lastSeq = cursor
    let fedThroughSeal = 0
    let after = cursor
    for (;;) {
      const page = await this.deps.storage.events.listAfter(match.id, after, EVENT_PAGE)
      for (const event of page) {
        if (!FED_EVENT_TYPES.has(event.type)) continue
        if (event.type !== 'turn.sealed' || event.payload.turn < fromTurn) {
          steps.push({ event })
          continue
        }
        const turn = event.payload.turn
        steps.push({ event, sealedOrders: await this.query.sealedOrders(match, turn) })
        sealSeqs.set(turn, event.seq)
        lastSeq = event.seq
        fedThroughSeal = steps.length
        if (
          turn >= through ||
          turn % SERVER_CHECKPOINT_EVERY_TURNS === 0 ||
          sealSeqs.size >= SEALS_PER_FEED
        ) {
          return { steps, sealSeqs, lastSeq }
        }
      }
      if (page.length < EVENT_PAGE) break
      after = page.at(-1)?.seq ?? after
    }
    // A feed ends on a seal, so that where it stops is a point the turn rows can name.
    return { steps: steps.slice(0, fedThroughSeal), sealSeqs, lastSeq }
  }

  /** The server's checkpoint of the state after `turn`, unless one with that hash is stored. */
  private async checkpoint(match: Match, turn: number, stateHash: string): Promise<void> {
    const resolver = this.deps.resolver
    if (!resolver) return
    try {
      const existing = await this.deps.storage.snapshots.getSummary(match.id, turn)
      if (existing?.stateHash === stateHash) return
      const snapshot = await resolver.snapshot(match.id)
      // Fed further by another caller since: that caller's checkpoint is the one to write.
      if (snapshot.stateHash !== stateHash) return
      await this.store(match, turn, snapshot)
    } catch (error) {
      this.deps.logger.warn('could not write the server checkpoint', {
        matchId: match.id,
        turn,
        error: String(error),
      })
    }
  }

  private async store(match: Match, turn: number, snapshot: ResolverSnapshot): Promise<void> {
    const description = await this.describe()
    if (!description) throw new Error('the turn resolver could not be reached')
    await this.deps.storage.snapshots.put({
      matchId: match.id,
      turn,
      formatVersion: description.snapshotFormatVersion,
      protocolVersion: MULTIPLAYER_PROTOCOL_VERSION,
      sessionVersion: match.sessionVersion,
      stateHash: snapshot.stateHash,
      uploadedByPlayerId: SERVER_SNAPSHOT_UPLOADER,
      uploadedAt: this.deps.clock.now(),
      body: snapshot.body,
    })
    // The prune keeps the newest turns, so a snapshot of an older turn written for a seat that
    // diverged there would be deleted before the seat could fetch it. The next newer write prunes.
    const latest = await this.deps.storage.snapshots.getLatestSummary(match.id)
    if (latest && latest.turn > turn) return
    try {
      await this.deps.storage.snapshots.prune(match.id, SNAPSHOTS_KEPT_PER_MATCH)
    } catch (error) {
      this.deps.logger.warn('could not prune old snapshots', {
        matchId: match.id,
        error: String(error),
      })
    }
  }
}

/** One seat's report of one state that differs from the server's, told once. */
const divergenceKey = (turn: number, playerId: string, reported: string): string =>
  `turn.diverged:${turn}:${playerId}:${reported}`

import { AuthService } from './AuthService'
import type { KernelDeps } from './deps'
import { EventPublisher } from './EventPublisher'
import { LobbyService, type LobbyServiceOptions } from './LobbyService'
import { MatchQueryService } from './MatchQueryService'
import { Referee } from './Referee'
import { type RetentionPolicy, RetentionService } from './RetentionService'
import { SeatViewService } from './SeatViewService'
import { SnapshotService } from './SnapshotService'
import { TurnService } from './TurnService'

export interface Kernel {
  deps: KernelDeps
  auth: AuthService
  query: MatchQueryService
  lobby: LobbyService
  turns: TurnService
  views: SeatViewService
  snapshots: SnapshotService
  referee: Referee
  retention: RetentionService
}

export interface KernelOptions extends LobbyServiceOptions {
  /** Overrides `DEFAULT_RETENTION`; a runtime maps its own configuration onto it. */
  retention?: RetentionPolicy
}

/** Wires the services once; both runtime facades call this with their own ports. */
export function createKernel(deps: KernelDeps, options: KernelOptions = {}): Kernel {
  const publisher = new EventPublisher(deps)
  const query = new MatchQueryService(deps.storage, (match) => referee.referees(match))
  const referee = new Referee(deps, query, publisher)
  const turns = new TurnService(deps, publisher, referee)
  return {
    deps,
    auth: new AuthService(deps.storage),
    query,
    lobby: new LobbyService(deps, publisher, turns, {
      ...options,
      seatViewsFor: (sessionVersion) => referee.offersSeatViews(sessionVersion),
    }),
    turns,
    views: new SeatViewService(deps, turns, referee),
    snapshots: new SnapshotService(deps, publisher, turns, referee),
    referee,
    retention: new RetentionService(deps, options.retention),
  }
}

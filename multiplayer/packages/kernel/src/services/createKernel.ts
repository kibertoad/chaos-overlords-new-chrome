import { AuthService } from './AuthService'
import type { KernelDeps } from './deps'
import { EventPublisher } from './EventPublisher'
import { LobbyService, type LobbyServiceOptions } from './LobbyService'
import { MatchQueryService } from './MatchQueryService'
import { type RetentionPolicy, RetentionService } from './RetentionService'
import { SnapshotService } from './SnapshotService'
import { TurnService } from './TurnService'

export interface Kernel {
  deps: KernelDeps
  auth: AuthService
  query: MatchQueryService
  lobby: LobbyService
  turns: TurnService
  snapshots: SnapshotService
  retention: RetentionService
}

export interface KernelOptions extends LobbyServiceOptions {
  /** Overrides `DEFAULT_RETENTION`; a runtime maps its own configuration onto it. */
  retention?: RetentionPolicy
}

/** Wires the services once; both runtime facades call this with their own ports. */
export function createKernel(deps: KernelDeps, options: KernelOptions = {}): Kernel {
  const publisher = new EventPublisher(deps)
  const turns = new TurnService(deps, publisher)
  return {
    deps,
    auth: new AuthService(deps.storage),
    query: new MatchQueryService(deps.storage),
    lobby: new LobbyService(deps, publisher, turns, options),
    turns,
    snapshots: new SnapshotService(deps, publisher, turns),
    retention: new RetentionService(deps, options.retention),
  }
}

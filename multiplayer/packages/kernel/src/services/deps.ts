import type {
  Clock,
  DeadlineScheduler,
  EventNotifier,
  Logger,
  StreamCloser,
} from '../ports/runtime'
import type { TurnResolver } from '../ports/resolver'
import type { MultiplayerStorage } from '../ports/storage'

/** Everything the services need, injected once by the runtime facade. */
export interface KernelDeps {
  storage: MultiplayerStorage
  notifier: EventNotifier
  scheduler: DeadlineScheduler
  /**
   * Where a revoked membership's open streams are hung up.
   *
   * Required rather than optional so a runtime cannot forget it: the stream fan-out lives outside
   * the kernel in both runtimes, and a silently missing closer is a kicked player who keeps reading
   * the match.
   */
  streams: StreamCloser
  clock: Clock
  logger: Logger
  /**
   * The game's own turn resolver, when the deployment runs one. With it the server resolves every
   * sealed turn of a match stored under the session version it plays, and its state decides the
   * turn (`Referee`). Without it, or for any other match, or while it fails, turns are decided by
   * the players' reports agreeing.
   */
  resolver?: TurnResolver
}

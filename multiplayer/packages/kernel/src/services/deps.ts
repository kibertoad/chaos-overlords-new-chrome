import type {
  Clock,
  DeadlineScheduler,
  EventNotifier,
  Logger,
  StreamCloser,
} from '../ports/runtime'
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
}

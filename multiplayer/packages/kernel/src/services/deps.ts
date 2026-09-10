import type { Clock, DeadlineScheduler, EventNotifier, Logger } from '../ports/runtime'
import type { MultiplayerStorage } from '../ports/storage'

/** Everything the services need, injected once by the runtime facade. */
export interface KernelDeps {
  storage: MultiplayerStorage
  notifier: EventNotifier
  scheduler: DeadlineScheduler
  clock: Clock
  logger: Logger
}

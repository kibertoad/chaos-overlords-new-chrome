import type { Principal } from '@chaos-overlords/kernel'
import type { ServerContainer } from '../container'

export interface AppEnv {
  Variables: {
    container: ServerContainer
    requestId: string
    principal: Principal
  }
}

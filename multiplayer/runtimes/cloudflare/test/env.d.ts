/// <reference types="@cloudflare/vitest-pool-workers/types" />
import type { D1Migration } from '@cloudflare/vitest-pool-workers'
import type { Env as WorkerEnv } from '../src/env'

// The pool types `env` from `cloudflare:test` as the ambient `Cloudflare.Env`; merge our bindings
// and the migration array injected in vitest.config.ts onto it.
declare global {
  namespace Cloudflare {
    interface Env extends WorkerEnv {
      TEST_MIGRATIONS: D1Migration[]
    }
  }
}

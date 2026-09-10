import type { Logger } from '@chaos-overlords/kernel'
import type { NodeConfig } from './config'

const ORDER = { debug: 0, info: 1, warn: 2, error: 3 } as const

/** JSON lines on stdout: what a systemd unit or a container log collector expects. */
export function createLogger(level: NodeConfig['logLevel'], sink = console): Logger {
  const threshold = ORDER[level]
  const emit = (severity: keyof typeof ORDER, msg: string, fields?: Record<string, unknown>) => {
    if (ORDER[severity] < threshold) return
    const line = JSON.stringify({ time: new Date().toISOString(), level: severity, msg, ...fields })
    if (severity === 'error') sink.error(line)
    else sink.log(line)
  }
  return {
    debug: (msg, fields) => emit('debug', msg, fields),
    info: (msg, fields) => emit('info', msg, fields),
    warn: (msg, fields) => emit('warn', msg, fields),
    error: (msg, fields) => emit('error', msg, fields),
  }
}

import type { PersistedEvent } from '../domain/entities'
import type { Clock, DeadlineScheduler, EventNotifier, Logger } from '../ports/runtime'

/** A clock tests advance by hand. */
export class ManualClock implements Clock {
  constructor(private current: Date = new Date('2026-01-01T00:00:00.000Z')) {}
  now(): Date {
    return new Date(this.current)
  }
  advance(ms: number): void {
    this.current = new Date(this.current.getTime() + ms)
  }
  set(date: Date): void {
    this.current = new Date(date)
  }
}

export class RecordingNotifier implements EventNotifier {
  readonly events: PersistedEvent[] = []
  async notify(event: PersistedEvent): Promise<void> {
    this.events.push(event)
  }
}

export class RecordingScheduler implements DeadlineScheduler {
  readonly scheduled: Array<{ matchId: string; turn: number; dueAt: Date }> = []
  async schedule(input: { matchId: string; turn: number; dueAt: Date }): Promise<void> {
    this.scheduled.push(input)
  }
}

export class RecordingLogger implements Logger {
  readonly lines: Array<{
    level: string
    msg: string
    fields: Record<string, unknown> | undefined
  }> = []
  debug(msg: string, fields?: Record<string, unknown>): void {
    this.lines.push({ level: 'debug', msg, fields })
  }
  info(msg: string, fields?: Record<string, unknown>): void {
    this.lines.push({ level: 'info', msg, fields })
  }
  warn(msg: string, fields?: Record<string, unknown>): void {
    this.lines.push({ level: 'warn', msg, fields })
  }
  error(msg: string, fields?: Record<string, unknown>): void {
    this.lines.push({ level: 'error', msg, fields })
  }
}

import type { ErrorCode } from '@chaos-overlords/contracts'

export type ErrorDetails = Record<string, unknown> & { reason?: string }

/** A refusal. Controllers throw these; the HTTP layer renders the one wire envelope. */
export class DomainError extends Error {
  constructor(
    readonly code: ErrorCode,
    message: string,
    readonly details: ErrorDetails = {},
  ) {
    super(message)
    this.name = new.target.name
  }
}

export class NotFoundError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('not_found', message, details)
  }
}
export class UnauthorizedError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('unauthorized', message, details)
  }
}
export class ForbiddenError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('forbidden', message, details)
  }
}
export class ConflictError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('conflict', message, details)
  }
}
export class ValidationError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('validation_failed', message, details)
  }
}
export class RateLimitedError extends DomainError {
  constructor(message: string, details: ErrorDetails = {}) {
    super('rate_limited', message, details)
  }
}

export function isDomainError(error: unknown): error is DomainError {
  return error instanceof DomainError
}

import { index, integer, primaryKey, sqliteTable, text } from 'drizzle-orm/sqlite-core'

/**
 * The SQLite dialect, shared byte-for-byte by better-sqlite3 (Node) and D1 (Cloudflare): one
 * migration lineage serves both. Timestamps are epoch milliseconds; JSON columns are text.
 * `name`, `visibility` and `max_players` are copied out of `settings` so the lobby list and the
 * seat-capacity check are plain SQL; `settings` stays the source clients read back. Every child
 * table cascades from `matches`, so retention is one delete of the rows a match owns.
 */
export const matches = sqliteTable(
  'matches',
  {
    id: text('id').primaryKey(),
    status: text('status').notNull(),
    name: text('name').notNull(),
    visibility: text('visibility').notNull(),
    maxPlayers: integer('max_players').notNull(),
    settings: text('settings', { mode: 'json' }).notNull(),
    hostPlayerId: text('host_player_id').notNull(),
    joinCode: text('join_code').notNull().unique(),
    passwordHash: text('password_hash'),
    seed: integer('seed'),
    currentTurn: integer('current_turn').notNull().default(0),
    seatCount: integer('seat_count').notNull().default(1),
    /** Monotonic: seats ever claimed. Never decremented, so `join_order` stays a total order. */
    joinCounter: integer('join_counter').notNull().default(1),
    createdAt: integer('created_at', { mode: 'timestamp_ms' }).notNull(),
    updatedAt: integer('updated_at', { mode: 'timestamp_ms' }).notNull(),
  },
  (table) => [
    // The sweep runs on a short interval and reads `matches` by status every tick: stalled seals of
    // live matches, and inactive ones past their retention age. Without these it scans the whole
    // table each time, which is the one cost that grows with every match ever played.
    index('matches_status_updated_idx').on(table.status, table.updatedAt),
    index('matches_lobby_idx').on(table.status, table.visibility, table.createdAt),
  ],
)

export const players = sqliteTable(
  'players',
  {
    id: text('id').primaryKey(),
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    slot: integer('slot').notNull().default(-1),
    joinOrder: integer('join_order').notNull().default(0),
    displayName: text('display_name').notNull(),
    /** Null once revoked; SQL equality never matches null, so a revoked token resolves to nobody. */
    tokenHash: text('token_hash').unique(),
    status: text('status').notNull(),
    joinedAt: integer('joined_at', { mode: 'timestamp_ms' }).notNull(),
  },
  (table) => [index('players_match_idx').on(table.matchId)],
)

export const turns = sqliteTable(
  'turns',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    number: integer('number').notNull(),
    status: text('status').notNull(),
    openedAt: integer('opened_at', { mode: 'timestamp_ms' }).notNull(),
    deadlineAt: integer('deadline_at', { mode: 'timestamp_ms' }),
    sealedAt: integer('sealed_at', { mode: 'timestamp_ms' }),
    orderSetHash: text('order_set_hash'),
    /** `[{ playerId, slot }]` frozen at seal time: exactly what `order_set_hash` was taken over. */
    sealedSlots: text('sealed_slots', { mode: 'json' }),
  },
  (table) => [
    primaryKey({ columns: [table.matchId, table.number] }),
    index('turns_deadline_idx').on(table.status, table.deadlineAt),
  ],
)

export const turnOrders = sqliteTable(
  'turn_orders',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    playerId: text('player_id').notNull(),
    orders: text('orders', { mode: 'json' }),
    ordersHash: text('orders_hash'),
    ready: integer('ready', { mode: 'boolean' }).notNull().default(false),
    submittedAt: integer('submitted_at', { mode: 'timestamp_ms' }),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn, table.playerId] })],
)

export const turnReports = sqliteTable(
  'turn_reports',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    playerId: text('player_id').notNull(),
    stateHash: text('state_hash').notNull(),
    finished: integer('finished', { mode: 'boolean' }).notNull().default(false),
    reportedAt: integer('reported_at', { mode: 'timestamp_ms' }).notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn, table.playerId] })],
)

export const snapshots = sqliteTable(
  'snapshots',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    formatVersion: integer('format_version').notNull(),
    stateHash: text('state_hash').notNull(),
    uploadedByPlayerId: text('uploaded_by_player_id').notNull(),
    uploadedAt: integer('uploaded_at', { mode: 'timestamp_ms' }).notNull(),
    body: text('body').notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn] })],
)

export const matchEvents = sqliteTable(
  'match_events',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    seq: integer('seq').notNull(),
    type: text('type').notNull(),
    payload: text('payload', { mode: 'json' }).notNull(),
    createdAt: integer('created_at', { mode: 'timestamp_ms' }).notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.seq] })],
)

import {
  bigint,
  boolean,
  index,
  integer,
  jsonb,
  pgTable,
  primaryKey,
  text,
  timestamp,
} from 'drizzle-orm/pg-core'

/** The Postgres dialect. Column-for-column the SQLite schema; see that file for the layout notes. */
const stamp = (name: string) => timestamp(name, { withTimezone: true, mode: 'date' })

export const matches = pgTable(
  'matches',
  {
    id: text('id').primaryKey(),
    status: text('status').notNull(),
    name: text('name').notNull(),
    visibility: text('visibility').notNull(),
    maxPlayers: integer('max_players').notNull(),
    settings: jsonb('settings').notNull(),
    hostPlayerId: text('host_player_id').notNull(),
    joinCode: text('join_code').notNull().unique(),
    passwordHash: text('password_hash'),
    /**
     * The seed is a SIGNED 32-bit integer, because `MatchSetup.InitialSeed` in the game core is a
     * C# `int`. It would fit Postgres' own signed `integer`; the column stays a `bigint` only so the
     * one migration already applied to a deployment does not have to be rewritten, and a wider
     * column costs nothing. The SQLite lineage shares the dialect-neutral `integer`.
     */
    seed: bigint('seed', { mode: 'number' }),
    currentTurn: integer('current_turn').notNull().default(0),
    seatCount: integer('seat_count').notNull().default(1),
    /** Monotonic: seats ever claimed. Never decremented, so `join_order` stays a total order. */
    joinCounter: integer('join_counter').notNull().default(1),
    createdAt: stamp('created_at').notNull(),
    updatedAt: stamp('updated_at').notNull(),
  },
  (table) => [
    // The sweep runs on a short interval and reads `matches` by status every tick: stalled seals of
    // live matches, and inactive ones past their retention age. Without these it scans the whole
    // table each time, which is the one cost that grows with every match ever played.
    index('matches_status_updated_idx').on(table.status, table.updatedAt),
    index('matches_lobby_idx').on(table.status, table.visibility, table.createdAt),
  ],
)

export const players = pgTable(
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
    joinedAt: stamp('joined_at').notNull(),
  },
  (table) => [index('players_match_idx').on(table.matchId)],
)

export const turns = pgTable(
  'turns',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    number: integer('number').notNull(),
    status: text('status').notNull(),
    openedAt: stamp('opened_at').notNull(),
    deadlineAt: stamp('deadline_at'),
    sealedAt: stamp('sealed_at'),
    orderSetHash: text('order_set_hash'),
    /** `[{ playerId, slot }]` frozen at seal time: exactly what `order_set_hash` was taken over. */
    sealedSlots: jsonb('sealed_slots'),
  },
  (table) => [
    primaryKey({ columns: [table.matchId, table.number] }),
    index('turns_deadline_idx').on(table.status, table.deadlineAt),
  ],
)

export const turnOrders = pgTable(
  'turn_orders',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    playerId: text('player_id').notNull(),
    orders: jsonb('orders'),
    ordersHash: text('orders_hash'),
    ready: boolean('ready').notNull().default(false),
    submittedAt: stamp('submitted_at'),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn, table.playerId] })],
)

export const turnReports = pgTable(
  'turn_reports',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    playerId: text('player_id').notNull(),
    stateHash: text('state_hash').notNull(),
    finished: boolean('finished').notNull().default(false),
    reportedAt: stamp('reported_at').notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn, table.playerId] })],
)

export const snapshots = pgTable(
  'snapshots',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    turn: integer('turn').notNull(),
    formatVersion: integer('format_version').notNull(),
    stateHash: text('state_hash').notNull(),
    uploadedByPlayerId: text('uploaded_by_player_id').notNull(),
    uploadedAt: stamp('uploaded_at').notNull(),
    body: text('body').notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.turn] })],
)

export const matchEvents = pgTable(
  'match_events',
  {
    matchId: text('match_id')
      .notNull()
      .references(() => matches.id, { onDelete: 'cascade' }),
    seq: integer('seq').notNull(),
    type: text('type').notNull(),
    payload: jsonb('payload').notNull(),
    createdAt: stamp('created_at').notNull(),
  },
  (table) => [primaryKey({ columns: [table.matchId, table.seq] })],
)

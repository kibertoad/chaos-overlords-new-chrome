CREATE TABLE "match_events" (
	"match_id" text NOT NULL,
	"seq" integer NOT NULL,
	"type" text NOT NULL,
	"payload" jsonb NOT NULL,
	"created_at" timestamp with time zone NOT NULL,
	CONSTRAINT "match_events_match_id_seq_pk" PRIMARY KEY("match_id","seq")
);
--> statement-breakpoint
CREATE TABLE "matches" (
	"id" text PRIMARY KEY NOT NULL,
	"status" text NOT NULL,
	"name" text NOT NULL,
	"visibility" text NOT NULL,
	"max_players" integer NOT NULL,
	"settings" jsonb NOT NULL,
	"host_player_id" text NOT NULL,
	"join_code" text NOT NULL,
	"password_hash" text,
	"seed" integer,
	"current_turn" integer DEFAULT 0 NOT NULL,
	"seat_count" integer DEFAULT 1 NOT NULL,
	"join_counter" integer DEFAULT 1 NOT NULL,
	"created_at" timestamp with time zone NOT NULL,
	"updated_at" timestamp with time zone NOT NULL,
	CONSTRAINT "matches_join_code_unique" UNIQUE("join_code")
);
--> statement-breakpoint
CREATE TABLE "players" (
	"id" text PRIMARY KEY NOT NULL,
	"match_id" text NOT NULL,
	"slot" integer DEFAULT -1 NOT NULL,
	"join_order" integer DEFAULT 0 NOT NULL,
	"display_name" text NOT NULL,
	"token_hash" text,
	"status" text NOT NULL,
	"joined_at" timestamp with time zone NOT NULL,
	CONSTRAINT "players_token_hash_unique" UNIQUE("token_hash")
);
--> statement-breakpoint
CREATE TABLE "snapshots" (
	"match_id" text NOT NULL,
	"turn" integer NOT NULL,
	"format_version" integer NOT NULL,
	"state_hash" text NOT NULL,
	"uploaded_by_player_id" text NOT NULL,
	"uploaded_at" timestamp with time zone NOT NULL,
	"body" text NOT NULL,
	CONSTRAINT "snapshots_match_id_turn_pk" PRIMARY KEY("match_id","turn")
);
--> statement-breakpoint
CREATE TABLE "turn_orders" (
	"match_id" text NOT NULL,
	"turn" integer NOT NULL,
	"player_id" text NOT NULL,
	"orders" jsonb,
	"orders_hash" text,
	"ready" boolean DEFAULT false NOT NULL,
	"submitted_at" timestamp with time zone,
	CONSTRAINT "turn_orders_match_id_turn_player_id_pk" PRIMARY KEY("match_id","turn","player_id")
);
--> statement-breakpoint
CREATE TABLE "turn_reports" (
	"match_id" text NOT NULL,
	"turn" integer NOT NULL,
	"player_id" text NOT NULL,
	"state_hash" text NOT NULL,
	"finished" boolean DEFAULT false NOT NULL,
	"reported_at" timestamp with time zone NOT NULL,
	CONSTRAINT "turn_reports_match_id_turn_player_id_pk" PRIMARY KEY("match_id","turn","player_id")
);
--> statement-breakpoint
CREATE TABLE "turns" (
	"match_id" text NOT NULL,
	"number" integer NOT NULL,
	"status" text NOT NULL,
	"opened_at" timestamp with time zone NOT NULL,
	"deadline_at" timestamp with time zone,
	"sealed_at" timestamp with time zone,
	"order_set_hash" text,
	"sealed_slots" jsonb,
	CONSTRAINT "turns_match_id_number_pk" PRIMARY KEY("match_id","number")
);
--> statement-breakpoint
ALTER TABLE "match_events" ADD CONSTRAINT "match_events_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "players" ADD CONSTRAINT "players_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "snapshots" ADD CONSTRAINT "snapshots_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "turn_orders" ADD CONSTRAINT "turn_orders_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "turn_reports" ADD CONSTRAINT "turn_reports_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "turns" ADD CONSTRAINT "turns_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "matches_status_updated_idx" ON "matches" USING btree ("status","updated_at");--> statement-breakpoint
CREATE INDEX "matches_lobby_idx" ON "matches" USING btree ("status","visibility","created_at");--> statement-breakpoint
CREATE INDEX "players_match_idx" ON "players" USING btree ("match_id");--> statement-breakpoint
CREATE INDEX "turns_deadline_idx" ON "turns" USING btree ("status","deadline_at");
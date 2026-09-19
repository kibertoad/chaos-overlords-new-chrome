ALTER TABLE "players" ADD COLUMN "portrait_id" integer DEFAULT 0 NOT NULL;--> statement-breakpoint
ALTER TABLE "matches" ADD COLUMN "session_version" integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE "snapshots" ADD COLUMN "session_version" integer DEFAULT 1 NOT NULL;

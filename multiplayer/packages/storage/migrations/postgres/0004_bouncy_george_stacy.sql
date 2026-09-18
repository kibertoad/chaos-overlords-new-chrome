ALTER TABLE "matches" ADD COLUMN "session_version" integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE "snapshots" ADD COLUMN "session_version" integer DEFAULT 1 NOT NULL;
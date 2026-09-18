ALTER TABLE "matches" ADD COLUMN "protocol_version" integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE "snapshots" ADD COLUMN "protocol_version" integer DEFAULT 1 NOT NULL;
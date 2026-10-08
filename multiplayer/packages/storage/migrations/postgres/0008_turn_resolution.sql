ALTER TABLE "turns" ADD COLUMN "resolved_hash" text;--> statement-breakpoint
ALTER TABLE "turns" ADD COLUMN "resolved_finished" boolean;--> statement-breakpoint
ALTER TABLE "turns" ADD COLUMN "resolved_seq" integer;
ALTER TABLE "match_events" ADD COLUMN "dedupe_key" text;--> statement-breakpoint
ALTER TABLE "takeover_prompts" ADD COLUMN "announced_at" timestamp with time zone;--> statement-breakpoint
ALTER TABLE "turns" ADD COLUMN "settled_at" timestamp with time zone;--> statement-breakpoint
CREATE UNIQUE INDEX "match_events_dedupe_idx" ON "match_events" USING btree ("match_id","dedupe_key");--> statement-breakpoint
CREATE INDEX "match_events_type_idx" ON "match_events" USING btree ("match_id","type","seq");--> statement-breakpoint
CREATE INDEX "turns_settled_idx" ON "turns" USING btree ("status","settled_at");--> statement-breakpoint
-- Backfill: an announcement logged before this column existed carries no key, so the first repeat
-- of it after the upgrade (the sweep finishing a seal whose successor never opened, say) would find
-- nothing under its key and log it a second time. The earliest event of each fact takes the key its
-- announcement is now published under; a fact the old code logged twice keeps its later copy unkeyed.
UPDATE "match_events" SET "dedupe_key" = 'turn.sealed:' || ("payload"->>'turn') WHERE "dedupe_key" IS NULL AND "type" = 'turn.sealed' AND "seq" = (SELECT min(e."seq") FROM "match_events" e WHERE e."match_id" = "match_events"."match_id" AND e."type" = 'turn.sealed' AND (e."payload"->>'turn') = ("match_events"."payload"->>'turn'));--> statement-breakpoint
UPDATE "match_events" SET "dedupe_key" = 'turn.confirmed:' || ("payload"->>'turn') WHERE "dedupe_key" IS NULL AND "type" = 'turn.confirmed' AND "seq" = (SELECT min(e."seq") FROM "match_events" e WHERE e."match_id" = "match_events"."match_id" AND e."type" = 'turn.confirmed' AND (e."payload"->>'turn') = ("match_events"."payload"->>'turn'));--> statement-breakpoint
UPDATE "match_events" SET "dedupe_key" = 'turn.desynced:' || ("payload"->>'turn') WHERE "dedupe_key" IS NULL AND "type" = 'turn.desynced' AND "seq" = (SELECT min(e."seq") FROM "match_events" e WHERE e."match_id" = "match_events"."match_id" AND e."type" = 'turn.desynced' AND (e."payload"->>'turn') = ("match_events"."payload"->>'turn'));--> statement-breakpoint
UPDATE "match_events" SET "dedupe_key" = 'match.statusChanged:finished' WHERE "dedupe_key" IS NULL AND "type" = 'match.statusChanged' AND "payload"->>'status' = 'finished' AND "seq" = (SELECT min(e."seq") FROM "match_events" e WHERE e."match_id" = "match_events"."match_id" AND e."type" = 'match.statusChanged' AND e."payload"->>'status' = 'finished');--> statement-breakpoint
-- Backfill: a prompt opened before this column existed was announced by the call that opened it.
-- Without a stamp it would read as still owing its announcement, and the next call to ask about
-- the seat would publish `match.takeoverVoteRequested` a second time.
UPDATE "takeover_prompts" SET "announced_at" = "opened_at" WHERE "announced_at" IS NULL;--> statement-breakpoint
-- Backfill: a turn confirmed before this column existed had its follow-ups run by the verdict that
-- confirmed it. Without a stamp every one of them would be handed to the sweep as a verdict cut
-- short, and `turn.confirmed` would be logged again for the whole history of every live match.
UPDATE "turns" SET "settled_at" = coalesce("sealed_at", now()) WHERE "status" = 'confirmed' AND "settled_at" IS NULL;

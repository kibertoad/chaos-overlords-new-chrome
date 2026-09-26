ALTER TABLE `match_events` ADD `dedupe_key` text;--> statement-breakpoint
CREATE UNIQUE INDEX `match_events_dedupe_idx` ON `match_events` (`match_id`,`dedupe_key`);--> statement-breakpoint
ALTER TABLE `takeover_prompts` ADD `announced_at` integer;--> statement-breakpoint
ALTER TABLE `turns` ADD `settled_at` integer;--> statement-breakpoint
CREATE INDEX `turns_settled_idx` ON `turns` (`status`,`settled_at`);--> statement-breakpoint
-- Backfill: a prompt opened before this column existed was announced by the call that opened it.
-- Without a stamp it would read as still owing its announcement, and the next call to ask about
-- the seat would publish `match.takeoverVoteRequested` a second time.
UPDATE `takeover_prompts` SET `announced_at` = `opened_at` WHERE `announced_at` IS NULL;--> statement-breakpoint
-- Backfill: a turn confirmed before this column existed had its follow-ups run by the verdict that
-- confirmed it. Without a stamp every one of them would be handed to the sweep as a verdict cut
-- short, and `turn.confirmed` would be logged again for the whole history of every live match.
UPDATE `turns` SET `settled_at` = coalesce(`sealed_at`, unixepoch() * 1000) WHERE `status` = 'confirmed' AND `settled_at` IS NULL;

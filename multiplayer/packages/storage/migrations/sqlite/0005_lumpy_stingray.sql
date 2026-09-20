ALTER TABLE `turns` ADD `desynced_at` integer;--> statement-breakpoint
-- Backfill: a turn that diverged before this column existed already announced itself. Without a
-- stamp the claim is still open, so the first sweep after the deploy would win it and publish
-- `turn.desynced` a second time for every paused match. `sealed_at` is set on every turn that can
-- reach `desynced`, and is the closest the row holds to when the verdict ran.
UPDATE `turns` SET `desynced_at` = coalesce(`sealed_at`, unixepoch() * 1000) WHERE `status` = 'desynced' AND `desynced_at` IS NULL;

CREATE INDEX `matches_status_updated_idx` ON `matches` (`status`,`updated_at`);--> statement-breakpoint
CREATE INDEX `matches_lobby_idx` ON `matches` (`status`,`visibility`,`created_at`);
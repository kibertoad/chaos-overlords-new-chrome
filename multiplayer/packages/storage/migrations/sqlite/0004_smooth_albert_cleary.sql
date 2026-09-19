ALTER TABLE `players` ADD `portrait_id` integer DEFAULT 0 NOT NULL;--> statement-breakpoint
ALTER TABLE `matches` ADD `session_version` integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE `snapshots` ADD `session_version` integer DEFAULT 1 NOT NULL;

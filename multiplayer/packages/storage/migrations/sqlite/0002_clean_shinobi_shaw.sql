ALTER TABLE `matches` ADD `protocol_version` integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE `snapshots` ADD `protocol_version` integer DEFAULT 1 NOT NULL;
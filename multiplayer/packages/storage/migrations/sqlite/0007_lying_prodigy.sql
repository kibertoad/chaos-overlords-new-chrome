CREATE TABLE `spectators` (
	`id` text PRIMARY KEY NOT NULL,
	`match_id` text NOT NULL,
	`display_name` text NOT NULL,
	`token_hash` text,
	`joined_at` integer NOT NULL,
	`left_at` integer,
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE UNIQUE INDEX `spectators_token_hash_unique` ON `spectators` (`token_hash`);--> statement-breakpoint
CREATE INDEX `spectators_match_idx` ON `spectators` (`match_id`);
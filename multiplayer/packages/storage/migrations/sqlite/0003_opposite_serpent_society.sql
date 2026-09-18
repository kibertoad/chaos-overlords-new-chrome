CREATE TABLE `takeover_prompts` (
	`match_id` text NOT NULL,
	`player_id` text NOT NULL,
	`turn` integer NOT NULL,
	`opened_at` integer NOT NULL,
	PRIMARY KEY(`match_id`, `player_id`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE TABLE `takeover_votes` (
	`match_id` text NOT NULL,
	`target_player_id` text NOT NULL,
	`voter_player_id` text NOT NULL,
	`decision` text NOT NULL,
	`cast_at` integer NOT NULL,
	PRIMARY KEY(`match_id`, `target_player_id`, `voter_player_id`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);

CREATE TABLE `match_events` (
	`match_id` text NOT NULL,
	`seq` integer NOT NULL,
	`type` text NOT NULL,
	`payload` text NOT NULL,
	`created_at` integer NOT NULL,
	PRIMARY KEY(`match_id`, `seq`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE TABLE `matches` (
	`id` text PRIMARY KEY NOT NULL,
	`status` text NOT NULL,
	`name` text NOT NULL,
	`visibility` text NOT NULL,
	`max_players` integer NOT NULL,
	`settings` text NOT NULL,
	`host_player_id` text NOT NULL,
	`join_code` text NOT NULL,
	`password_hash` text,
	`seed` integer,
	`current_turn` integer DEFAULT 0 NOT NULL,
	`seat_count` integer DEFAULT 1 NOT NULL,
	`join_counter` integer DEFAULT 1 NOT NULL,
	`created_at` integer NOT NULL,
	`updated_at` integer NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `matches_join_code_unique` ON `matches` (`join_code`);--> statement-breakpoint
CREATE INDEX `matches_status_updated_idx` ON `matches` (`status`,`updated_at`);--> statement-breakpoint
CREATE INDEX `matches_lobby_idx` ON `matches` (`status`,`visibility`,`created_at`);--> statement-breakpoint
CREATE TABLE `players` (
	`id` text PRIMARY KEY NOT NULL,
	`match_id` text NOT NULL,
	`slot` integer DEFAULT -1 NOT NULL,
	`join_order` integer DEFAULT 0 NOT NULL,
	`display_name` text NOT NULL,
	`token_hash` text,
	`status` text NOT NULL,
	`joined_at` integer NOT NULL,
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE UNIQUE INDEX `players_token_hash_unique` ON `players` (`token_hash`);--> statement-breakpoint
CREATE INDEX `players_match_idx` ON `players` (`match_id`);--> statement-breakpoint
CREATE TABLE `snapshots` (
	`match_id` text NOT NULL,
	`turn` integer NOT NULL,
	`format_version` integer NOT NULL,
	`state_hash` text NOT NULL,
	`uploaded_by_player_id` text NOT NULL,
	`uploaded_at` integer NOT NULL,
	`body` text NOT NULL,
	PRIMARY KEY(`match_id`, `turn`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE TABLE `turn_orders` (
	`match_id` text NOT NULL,
	`turn` integer NOT NULL,
	`player_id` text NOT NULL,
	`orders` text,
	`orders_hash` text,
	`ready` integer DEFAULT false NOT NULL,
	`submitted_at` integer,
	PRIMARY KEY(`match_id`, `turn`, `player_id`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE TABLE `turn_reports` (
	`match_id` text NOT NULL,
	`turn` integer NOT NULL,
	`player_id` text NOT NULL,
	`state_hash` text NOT NULL,
	`finished` integer DEFAULT false NOT NULL,
	`reported_at` integer NOT NULL,
	PRIMARY KEY(`match_id`, `turn`, `player_id`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE TABLE `turns` (
	`match_id` text NOT NULL,
	`number` integer NOT NULL,
	`status` text NOT NULL,
	`opened_at` integer NOT NULL,
	`deadline_at` integer,
	`sealed_at` integer,
	`order_set_hash` text,
	`sealed_slots` text,
	PRIMARY KEY(`match_id`, `number`),
	FOREIGN KEY (`match_id`) REFERENCES `matches`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `turns_deadline_idx` ON `turns` (`status`,`deadline_at`);
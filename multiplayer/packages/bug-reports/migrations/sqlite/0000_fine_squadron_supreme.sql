CREATE TABLE `bug_reports` (
	`id` text PRIMARY KEY NOT NULL,
	`received_at` integer NOT NULL,
	`message` text NOT NULL,
	`client_version` text NOT NULL,
	`client_platform` text NOT NULL,
	`context` text,
	`state_codec` text,
	`state_format_version` integer,
	`state_uncompressed_bytes` integer,
	`state_compressed_bytes` integer,
	`state_sha256` text,
	`state_anonymized` integer,
	`blob_key` text,
	`body` text
);
--> statement-breakpoint
CREATE INDEX `bug_reports_received_idx` ON `bug_reports` (`received_at`);
CREATE TABLE "rate_limit_windows" (
	"key" text PRIMARY KEY NOT NULL,
	"window_start" bigint NOT NULL,
	"reset_at" bigint NOT NULL,
	"count" integer NOT NULL,
	"allowed" boolean NOT NULL
);
--> statement-breakpoint
CREATE INDEX "rate_limit_windows_reset_idx" ON "rate_limit_windows" USING btree ("reset_at");
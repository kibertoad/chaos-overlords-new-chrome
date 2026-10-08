CREATE TABLE "spectators" (
	"id" text PRIMARY KEY NOT NULL,
	"match_id" text NOT NULL,
	"display_name" text NOT NULL,
	"token_hash" text,
	"joined_at" timestamp with time zone NOT NULL,
	"left_at" timestamp with time zone,
	CONSTRAINT "spectators_token_hash_unique" UNIQUE("token_hash")
);
--> statement-breakpoint
ALTER TABLE "spectators" ADD CONSTRAINT "spectators_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "spectators_match_idx" ON "spectators" USING btree ("match_id");
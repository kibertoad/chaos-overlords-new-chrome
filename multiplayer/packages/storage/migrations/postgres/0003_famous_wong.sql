CREATE TABLE "takeover_prompts" (
	"match_id" text NOT NULL,
	"player_id" text NOT NULL,
	"turn" integer NOT NULL,
	"opened_at" timestamp with time zone NOT NULL,
	CONSTRAINT "takeover_prompts_match_id_player_id_pk" PRIMARY KEY("match_id","player_id")
);
--> statement-breakpoint
CREATE TABLE "takeover_votes" (
	"match_id" text NOT NULL,
	"target_player_id" text NOT NULL,
	"voter_player_id" text NOT NULL,
	"decision" text NOT NULL,
	"cast_at" timestamp with time zone NOT NULL,
	CONSTRAINT "takeover_votes_match_id_target_player_id_voter_player_id_pk" PRIMARY KEY("match_id","target_player_id","voter_player_id")
);
--> statement-breakpoint
ALTER TABLE "takeover_prompts" ADD CONSTRAINT "takeover_prompts_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "takeover_votes" ADD CONSTRAINT "takeover_votes_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;
CREATE TABLE "removal_votes" (
	"match_id" text NOT NULL,
	"target_player_id" text NOT NULL,
	"voter_player_id" text NOT NULL,
	"decision" text NOT NULL,
	"cast_at" timestamp with time zone NOT NULL,
	CONSTRAINT "removal_votes_match_id_target_player_id_voter_player_id_pk" PRIMARY KEY("match_id","target_player_id","voter_player_id")
);
--> statement-breakpoint
ALTER TABLE "removal_votes" ADD CONSTRAINT "removal_votes_match_id_matches_id_fk" FOREIGN KEY ("match_id") REFERENCES "public"."matches"("id") ON DELETE cascade ON UPDATE no action;
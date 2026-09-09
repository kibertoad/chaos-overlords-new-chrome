using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawFinance(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        var player = state.FindPlayer(playerId)!;
        var forecast = EconomyResolver.Project(state, player);
        font.Draw(batch, "FINANCIAL", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, player.Setup.Name, new Vector2(18, 86), PlayerColors[playerId.Value], 1);
        string[] rows =
        [
            $"CURRENT CASH       ${forecast.CurrentCash}",
            $"SECTOR TAXES       +${forecast.SectorIncome}",
            $"SITE INCOME        +${forecast.SiteIncome}",
            $"GANG UPKEEP        -${forecast.GangUpkeep}",
            "---------------------------",
            $"NEXT BALANCE       ${forecast.ResultCash}",
            $"NET CHANGE         {Signed(forecast.NetChange)}",
            "",
            $"TOTAL CASH EARNED  ${player.Statistics.CashEarned}",
            $"TOTAL CASH SPENT   ${player.Statistics.CashSpent}"
        ];
        for (var index = 0; index < rows.Length; index++)
            font.Draw(batch, rows[index], new Vector2(18, 120 + index * 22), Color.White, 1);
        font.Draw(batch, "PROJECTED AT NEXT UPKEEP", new Vector2(18, 368), new Color(180, 230, 170), 1);
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawSearch(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var playerId = state.Coordinator.ActivePlayer ?? new PlayerId(0);
        font.Draw(batch, $"SEARCH SECTOR {_cursor + 1}", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, state.FindPlayer(playerId)!.Setup.Name, new Vector2(18, 86),
            PlayerColors[playerId.Value], 1);
        var visible = SectorGangView.Visible(state, playerId, _cursor);
        if (visible.Count == 0)
            font.Draw(batch, "NO GANGS DETECTED", new Vector2(18, 116), Color.White, 1);
        foreach (var entry in visible.Take(SectorGangView.MaximumSearchRows)
                     .Select((gang, index) => (gang, index)))
        {
            var gang = entry.gang;
            var definition = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId);
            var owner = state.FindPlayer(gang.Owner)!;
            var y = 112 + entry.index * 40;
            var portrait = SectorGangView.SearchPortrait(entry.index);
            if (_gangPortraits is not null)
                batch.Draw(_gangPortraits, portrait,
                    OriginalSpriteLayout.GangPortrait(definition.Id), Color.White);
            DrawBorder(batch, pixel, portrait, PlayerColors[gang.Owner.Value], 1);
            font.Draw(batch, definition.Name, new Vector2(62, y), PlayerColors[gang.Owner.Value], 1);
            font.Draw(batch, $"{owner.Setup.Name}  FORCE {gang.Force}"
                + (gang.Hidden ? "  HIDDEN" : ""), new Vector2(190, y), Color.White, 1);
        }
        if (visible.Count > SectorGangView.MaximumSearchRows)
            font.Draw(batch, $"+{visible.Count - SectorGangView.MaximumSearchRows} MORE",
                new Vector2(18, 390), Color.White, 1);
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawRanking(SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state)
    {
        DrawManagementPanel(batch, pixel);
        var scenario = ScenarioCatalog.Get(state.Setup.Scenario);
        font.Draw(batch, "RANKING", new Vector2(18, 60), Color.Gold, 2);
        font.Draw(batch, scenario.Name, new Vector2(18, 86), Color.White, 1);
        font.Draw(batch, scenario.Objective.ToUpperInvariant(), new Vector2(18, 104), new Color(180, 230, 170), 1);

        if (scenario.IsTimed)
        {
            var standings = EndgameRankingEvaluator.EvaluateTimed(state);
            foreach (var entry in standings.Select((standing, index) => (standing, index)))
            {
                var player = state.FindPlayer(entry.standing.Player)!;
                var y = 142 + entry.index * 38;
                font.Draw(batch, $"{entry.standing.Place}. {player.Setup.Name}", new Vector2(18, y),
                    PlayerColors[player.Id.Value], 1);
                font.Draw(batch, $"SCORE {entry.standing.Score}", new Vector2(234, y), Color.White, 1);
            }
            var turns = ScenarioCatalog.Turns(state.Setup.Duration);
            font.Draw(batch, $"TURN {state.Coordinator.Turn} OF {turns}", new Vector2(18, 382), Color.White, 1);
        }
        else
        {
            foreach (var entry in state.Players.OrderBy(player => player.Id.Value)
                         .Select((player, index) => (player, index)))
            {
                var score = MatchOutcomeEvaluator.Project(state, entry.player);
                var y = 142 + entry.index * 38;
                font.Draw(batch, entry.player.Setup.Name, new Vector2(18, y),
                    PlayerColors[entry.player.Id.Value], 1);
                font.Draw(batch, ObjectiveProgress(state.Setup.Scenario, score),
                    new Vector2(170, y), Color.White, 1);
            }
        }
        DrawButton(batch, pixel, font, ManagementBack, "BACK", false);
    }

    private void DrawManagementPanel(SpriteBatch batch, Texture2D pixel)
    {
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        batch.Draw(pixel, new Rectangle(8, 48, 420, 402), new Color(0, 0, 0, 235));
    }

    private static string Signed(int value) => value >= 0 ? $"+${value}" : $"-${Math.Abs(value)}";

    private static string ObjectiveProgress(ScenarioId scenario, PlayerScoreState score) => scenario switch
    {
        ScenarioId.KillEmAll => score.IsAlive ? $"ALIVE  FOES {score.OpponentsAlive}" : "ELIMINATED",
        ScenarioId.Big40 => $"SECTORS {score.ControlledSectors}/40",
        ScenarioId.Eliminate => $"ENEMY RIGHT HANDS {score.OpposingRightHandsAlive}",
        ScenarioId.Siege => $"IMPORTANT {score.ImportantSectorsControlled}/6",
        ScenarioId.BigMan => $"POINTS {score.BigManPoints}/40",
        ScenarioId.Armageddon => $"SECTORS {score.ControlledSectors}/{MatchLimits.SectorCount}",
        _ => ""
    };
}

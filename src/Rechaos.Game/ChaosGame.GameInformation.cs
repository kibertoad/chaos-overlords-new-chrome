using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawGameInformation(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_managementReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(batch, pixel, font, state);
        else
            DrawBoard(batch, pixel, font, state);

        if (_gameInfoBackground is not null)
            batch.Draw(_gameInfoBackground, GameInformationLayout.Panel, Color.White);
        else
            batch.Draw(pixel, GameInformationLayout.Panel, new Color(0, 0, 0, 245));

        ClearGameInformationFields(batch, pixel);
        font.Draw(batch, ScenarioCatalog.Get(state.Setup.Scenario).Name,
            new Vector2(GameInformationLayout.ValueLeft, GameInformationLayout.ObjectiveY),
            Color.Lime, 1);
        font.Draw(batch, DifficultyPresentation.Label(state.Setup.AiMentality),
            new Vector2(GameInformationLayout.ValueLeft, GameInformationLayout.AiMentalityY),
            Color.Lime, 1);
        font.Draw(batch, PlanningTimerPolicy.Label(_selectedPlanningTimeLimit),
            new Vector2(GameInformationLayout.ValueLeft, GameInformationLayout.TurnTimeLimitY),
            Color.Lime, 1);

        foreach (var player in state.Setup.Players)
        {
            var row = player.Id.Value;
            var y = GameInformationLayout.PlayerY(row);
            batch.Draw(pixel, GameInformationLayout.PlayerColor(row), PlayerColors[row]);
            font.Draw(batch, player.Name,
                new Vector2(GameInformationLayout.PlayerNameLeft, y), Color.Lime, 1);
            DrawPanelValue(font, batch,
                GameInformationPresentation.Intelligence(player.Controller),
                GameInformationLayout.IntelligenceRight, y);
        }
    }

    private static void ClearGameInformationFields(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, new Rectangle(GameInformationLayout.ValueLeft,
            GameInformationLayout.ObjectiveY, 180, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(GameInformationLayout.ValueLeft,
            GameInformationLayout.AiMentalityY, 180, 7), Color.Black);
        batch.Draw(pixel, new Rectangle(GameInformationLayout.ValueLeft,
            GameInformationLayout.TurnTimeLimitY, 180, 7), Color.Black);
        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            var y = GameInformationLayout.PlayerY(player);
            batch.Draw(pixel, new Rectangle(GameInformationLayout.PlayerNameLeft, y, 90, 7), Color.Black);
            batch.Draw(pixel, new Rectangle(348, y, 36, 7), Color.Black);
        }
    }
}

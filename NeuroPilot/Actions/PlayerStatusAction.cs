using System;
using System.Text;
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class PlayerStatusAction : NeuroAction
    {
        public override string Name => "player_status";

        protected override string Description => "Gives some general information about the status of the player.";

        protected override JsonSchema Schema => new();

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            StringBuilder statusBuilder = new($"Wearing Suit: {PlayerState.IsWearingSuit()}\n");
            // Some information is only visible when the player is wearing a suit
            if (PlayerState.IsWearingSuit())
            {
                var playerResources = Locator.GetPlayerBody().GetComponent<PlayerResources>();
                statusBuilder.Append($"Health: {Math.Round(playerResources.GetHealthFraction() * 100)}%\n");
                statusBuilder.Append($"Fuel: {Math.Round(playerResources.GetFuelFraction() * 100)}%\n");
                statusBuilder.Append($"Oxygen: {Math.Round(playerResources.GetOxygenFraction() * 100)}%\n");
            }
            statusBuilder.Append($"Inside Ship: {PlayerState.IsInsideShip()}\n");
            return ExecutionResult.Success(statusBuilder.ToString());
        }

        protected override async UniTask ExecuteAsync()
        {
            await UniTask.CompletedTask;
        }
    }
}
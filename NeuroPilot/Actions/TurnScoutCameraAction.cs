
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;

namespace NeuroPilot.Actions
{
    public class TurnScoutCameraAction : NeuroAction<ActionJData>
    {
        public override string Name => "turn_scout_camera";
        protected override string Description => "Turn the scout camera in a specified direction. You can optionally specify the amount of steps the camera should turn (1 step is 30 degrees, up to 12 steps at a time).";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = ["direction"],
            Properties = new Dictionary<string, JsonSchema>
            {
                ["direction"] = QJS.Enum(["left", "right", "up", "down"]),
                ["steps"] = new JsonSchema { Type = JsonSchemaType.Integer },
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData, out ActionJData actionDataOut)
        {
            actionDataOut = actionData;
            if (Locator.GetProbe() == null || !Locator.GetProbe().IsAnchored())
            {
                return ExecutionResult.Failure("Scout not found or not anchored. Please wait until the probe has landed after launching it before trying to turn the camera.");
            }
            string direction = actionData.Data?["direction"]?.Value<string>();
            if (string.IsNullOrEmpty(direction) || !new List<string> { "left", "right", "up", "down" }.Contains(direction))
            {
                return ExecutionResult.Failure("Action failed. Invalid direction specified.");
            }
            int amount = actionData.Data?["steps"]?.Value<int>() ?? 1;
            if (amount < 1 || amount > 12)
            {
                return ExecutionResult.Failure("Action failed. Amount must be between 1 and 12.");
            }
            return ExecutionResult.Success($"Turned scout camera {direction}.");

        }

        protected override async UniTask ExecuteAsync(ActionJData actionData)
        {
            string direction = actionData.Data?["direction"]?.Value<string>();
            int amount = actionData.Data?["steps"]?.Value<int>() ?? 1;
            await ScoutPatches.TurnSurveyorProbeAsync(ScoutPatches.probeLauncher.GetActiveProbe(), direction, amount);
        }
    }
}
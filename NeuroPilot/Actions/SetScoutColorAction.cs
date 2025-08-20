
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroPilot.Actions
{
    public class SetLightColorAction : NeuroAction
    {
        public override string Name => "set_light_color";

        protected override string Description => "Set the color in hex format (#RRGGBB) and optional brightness from 0 to 3 (default: 1) of the scout's lights.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = ["color"],
            Properties = new Dictionary<string, JsonSchema>
            {
                ["color"] = new JsonSchema { Type = JsonSchemaType.String },
                ["brightness"] = new JsonSchema { Type = JsonSchemaType.Integer }
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (!ColorUtility.TryParseHtmlString(actionData.Data?["color"]?.ToString(), out Color color))
                return ExecutionResult.Failure("Invalid color format. Use hex format (e.g., #RRGGBB).");
            int intensity = actionData.Data?["brightness"]?.ToObject<int>() ?? 1;
            if (intensity < 0 || intensity > 3)
                return ExecutionResult.Failure("Brightness must be between 0 and 3.");

            ScoutPatches.surveyorProbeColor = color;
            ScoutPatches.surveyorProbeIntensity = intensity;
            return ExecutionResult.Success("Scout's lights updated successfully.");
        }

        protected override async UniTask ExecuteAsync()
        {
            ScoutPatches.UpdateSurveyProbeLights();
            ScoutPatches.UpdateShipLights();
            await UniTask.CompletedTask;
        }
    }
}
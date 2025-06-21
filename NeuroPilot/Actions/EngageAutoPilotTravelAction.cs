using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroPilot.Actions
{
    public class EngageAutoPilotTravelAction : NeuroAction<string>
    {
        const string destinationPropName = "destination";

        public override string Name => "engageAutoPilotTravel";

        protected override string Description => "Activates the ship's autopilot to fly to a specific destination in the solar system.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [destinationPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { destinationPropName, QJS.Enum(Destinations.GetAllValidNames()) },
            },
        };

        protected override ExecutionResult Validate(ActionJData actionData, out string parsedData)
        {
            var destinationName = actionData.Data?[destinationPropName]?.ToString();
            parsedData = destinationName ?? string.Empty;

            if (string.IsNullOrEmpty(destinationName))
            {
                return ExecutionResult.Failure($"`{destinationPropName}` is required.");
            }

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be engaged while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryEngageTravel(destinationName, out var error))
            {
                return ExecutionResult.Failure(error);
            }


            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(string parsedData)
        {
            // It's too late to signal if autopilot failed to engage so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

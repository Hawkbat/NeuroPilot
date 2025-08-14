using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;

namespace NeuroPilot.Actions
{
    public class OrbitToLocationAction : NeuroAction<(string, string)>
    {
        const string destinationPropName = "destination";
        const string locationPropName = "location";

        public override string Name => "initiate_orbit_to_location";

        protected override string Description => "Starts the process of flying to a specific location on a specific destination planet in the solar system.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [destinationPropName, locationPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { destinationPropName, QJS.Enum(Destinations.GetAllValidNames()) },
                { locationPropName, new JsonSchema { Type = JsonSchemaType.String } },
            },
        };

        protected override ExecutionResult Validate(ActionJData actionData, out (string, string) names)
        {
            var destinationProp = actionData.Data?[destinationPropName]?.ToString();
            var destinationName = destinationProp ?? string.Empty;

            var locationProp = actionData.Data?[locationPropName]?.ToString();
            var locationName = locationProp ?? string.Empty;

            names = (destinationName, locationName);

            if (string.IsNullOrEmpty(destinationProp))
            {
                return ExecutionResult.Failure($"`{destinationPropName}` is required.");
            }

            if (string.IsNullOrEmpty(locationProp))
            {
                return ExecutionResult.Failure($"`{locationPropName}` is required.");
            }

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be engaged while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryOrbitToLocation(destinationName, locationName, out var error))
            {
                return ExecutionResult.Failure(error);
            }


            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync((string, string) parsedData)
        {
            // It's too late to signal if autopilot failed to engage so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

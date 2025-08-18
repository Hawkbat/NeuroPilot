using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;

namespace NeuroPilot.Actions
{
    public class TravelAction : NeuroAction<(string, string)>
    {
        const string destinationPropName = "destination";
        const string locationPropName = "location";

        public override string Name => "travel_to_location";

        protected override string Description => "Starts the process of flying to a specific destination planet in the solar system. And optionally specific location on the planet.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [destinationPropName],
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

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be engaged while in-game.");
            }

            string error;

            if (string.IsNullOrEmpty(locationProp))
            {
                if (!EnhancedAutoPilot.GetInstance().TryEngageTravel(destinationName, out error))
                {
                    return ExecutionResult.Failure(error);
                }

                return ExecutionResult.Success();
            }

            if (!EnhancedAutoPilot.GetInstance().TryOrbitToLocation(destinationName, locationName, out error))
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

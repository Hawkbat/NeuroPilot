using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System.Collections.Generic;
using System.Linq;

namespace NeuroPilot.Actions
{
    public class GetDestinationLocationsAction : NeuroAction<Destination>
    {
        const string destinationPropName = "destination";

        public override string Name => "get_locations_on_destination";

        protected override string Description => "Gets a list of known locations on a destination planet that can be orbited to.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [destinationPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { destinationPropName, QJS.Enum(Destinations.GetRegisteredNames()) },
            },
        };

        protected override ExecutionResult Validate(ActionJData actionData, out Destination destination)
        {
            var destinationProp = actionData.Data?[destinationPropName]?.ToString();
            var destinationName = destinationProp ?? string.Empty;
            destination = null;

            if (string.IsNullOrEmpty(destinationProp))
            {
                return ExecutionResult.Failure($"`{destinationPropName}` is required.");
            }

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be engaged while in-game.");
            }

            destination = Destinations.GetByName(destinationName);
            if (destination == null)
            {
                return ExecutionResult.Failure($"Invalid destination '{destination.Name}'. Valid destinations are: {string.Join(", ", Destinations.GetAllValidNames())}");
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(Destination destination)
        {
            var locationNames = string.Join(", ", Locations.ByDestination(destination).Select(pair => pair.Item1));
            Context.Send($"Valid locations: {locationNames}");
            return UniTask.CompletedTask;
        }
    }
}

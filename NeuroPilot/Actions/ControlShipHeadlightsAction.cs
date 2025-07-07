using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace NeuroPilot.Actions
{
    public class ControlShipHeadlightsAction : NeuroAction<bool>
    {
        const string onPropName = "on";

        public override string Name => "controlShipHeadlights";

        protected override string Description => "Turns the ship headlights on or off.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [onPropName],
            Properties = new Dictionary<string, JsonSchema>
            {
                { onPropName, new JsonSchema { Type = JsonSchemaType.Boolean } }
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData, out bool on)
        {
            var onProp = actionData.Data?[onPropName]?.Value<bool>();
            if (!onProp.HasValue)
            {
                on = false;
                return ExecutionResult.Failure($"`{onPropName}` is required and must be a boolean.");
            }

            on = onProp.Value;

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Ship headlights can only be controlled while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryControlHeadlights(on, out var error))
            {
                return ExecutionResult.Failure(error);
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(bool on)
        {
            // Action was executed in Validate instead. Technically incorrect but reduces latency and allows for direct feedback.
            return UniTask.CompletedTask;
        }
    }
}

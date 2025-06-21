using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroPilot.Actions
{
    public class SetShipHeadlightsAction : NeuroAction<bool>
    {
        const string enabledPropName = "enabled";

        public override string Name => "setShipHeadlights";

        protected override string Description => "Turns the ship headlights on or off.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [enabledPropName],
            Properties = new Dictionary<string, JsonSchema>
            {
                { enabledPropName, new JsonSchema { Type = JsonSchemaType.Boolean } }
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData, out bool enabled)
        {
            var enabledProp = actionData.Data?[enabledPropName]?.Value<bool>();
            if (!enabledProp.HasValue)
            {
                enabled = false;
                return ExecutionResult.Failure($"`{enabledPropName}` is required and must be a boolean.");
            }

            enabled = enabledProp.Value;

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Ship headlights can only be set while in-game.");
            }

            var cockpitController = Locator.GetShipTransform().GetComponentInChildren<ShipCockpitController>();
            if (cockpitController._shipSystemFailure)
            {
                return ExecutionResult.Failure("Cannot set ship headlights while the ship is damaged.");
            }

            if (cockpitController._externalLightsOn == enabled)
            {
                // Silently ignore if the headlights are already in the desired state.
                return ExecutionResult.Success();
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(bool enabled)
        {
            var cockpitController = Locator.GetShipTransform().GetComponentInChildren<ShipCockpitController>();
            cockpitController._externalLightsOn = enabled;
            cockpitController.SetEnableShipLights(enabled);
            return UniTask.CompletedTask;
        }
    }
}

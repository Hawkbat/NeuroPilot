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

            var cockpitController = Locator.GetShipTransform().GetComponentInChildren<ShipCockpitController>();
            if (cockpitController._shipSystemFailure)
            {
                return ExecutionResult.Failure("Cannot control ship headlights while the ship is damaged.");
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(bool on)
        {
            var cockpitController = Locator.GetShipTransform().GetComponentInChildren<ShipCockpitController>();

            if (cockpitController._shipSystemFailure)
            {
                // If the ship is damaged, we cannot control the headlights. Silent failure.
                return UniTask.CompletedTask;
            }

            if (cockpitController._externalLightsOn != on)
            {
                cockpitController._externalLightsOn = on;
                cockpitController.SetEnableShipLights(on);
            }

            return UniTask.CompletedTask;
        }
    }
}

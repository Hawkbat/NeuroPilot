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
    public class ControlShipHatchAction : NeuroAction<bool>
    {
        const string openPropName = "open";

        public override string Name => "controlShipHatch";

        protected override string Description => "Opens or closes the external hatch, which the player uses to enter and exit the ship.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [openPropName],
            Properties = new Dictionary<string, JsonSchema>
            {
                { openPropName, new JsonSchema { Type = JsonSchemaType.Boolean } }
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData, out bool open)
        {
            var openProp = actionData.Data?[openPropName]?.Value<bool>();
            if (!openProp.HasValue)
            {
                open = false;
                return ExecutionResult.Failure($"`{openPropName}` is required and must be a boolean.");
            }

            open = openProp.Value;

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Ship hatch can only be opened or closed while in-game.");
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(bool open)
        {
            var hatchController = Locator.GetShipTransform().GetComponentInChildren<HatchController>();
            if (open) hatchController.OpenHatch();
            else hatchController.CloseHatch();
            return UniTask.CompletedTask;
        }
    }
}

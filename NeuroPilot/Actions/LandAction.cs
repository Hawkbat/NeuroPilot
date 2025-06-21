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
    public class LandAction : NeuroAction
    {
        public override string Name => "land";

        protected override string Description => "Uses the ship's autopilot to land at the current location.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
        };

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be used while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryLand(out var error))
            {
                return ExecutionResult.Failure(error);
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync()
        {
            // It's too late to signal if autopilot failed to activate so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

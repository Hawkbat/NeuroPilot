using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class AbortAutoPilotAction : NeuroAction
    {
        public override string Name => "abort_auto_pilot";

        protected override string Description => "Deactivates the ship's autopilot while in flight, returning control to the player.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
        };

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be aborted while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryAbortTravel(out var error))
            {
                return ExecutionResult.Failure(error);
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync()
        {
            // It's too late to signal if autopilot failed to abort so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

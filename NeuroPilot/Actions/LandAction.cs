using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class LandAction : NeuroAction
    {
        public override string Name => "initiate_landing";

        protected override string Description => "Starts the process of landing the ship at the current location.";

        protected override JsonSchema Schema => new();

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

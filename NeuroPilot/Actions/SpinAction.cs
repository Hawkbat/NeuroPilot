using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class SpinAction : NeuroAction
    {
        public override string Name => "spin";

        protected override string Description => "Spin the ship.";

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

            if (!EnhancedAutoPilot.GetInstance().Spin(out var error))
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

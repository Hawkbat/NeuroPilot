
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class LauchScoutAction : NeuroAction
    {
        public override string Name => "launch_scout";

        protected override string Description => "Use the scout launcher to launch a scout.";

        protected override JsonSchema Schema => new();

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (ScoutPatches.probeLauncher == null) return ExecutionResult.Failure("Scout launcher is not equipped.");
            if (ScoutPatches.probeLauncher.GetActiveProbe() != null) return ExecutionResult.Failure("Scout is already launched. Please retrieve it before launching a new one.");
            return ExecutionResult.Success("Scout launched.");
        }

        protected override async UniTask ExecuteAsync()
        {
            ScoutPatches.probeLauncher.LaunchProbe();
            await UniTask.CompletedTask;
        }
    }
}
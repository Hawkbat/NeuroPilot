
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class RetrieveScoutAction : NeuroAction
    {
        public override string Name => "retrieve_scout";

        protected override string Description => "Retrieve the scout that was launched from the scout launcher.";
        protected override JsonSchema Schema => new();
        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (ScoutPatches.probeLauncher == null) return ExecutionResult.Failure("Scout not found.");
            return ExecutionResult.Success("Scout retrieved.");
        }

        protected override async UniTask ExecuteAsync()
        {
            ScoutPatches.probeLauncher.RetrieveProbe();
            await UniTask.CompletedTask;
        }
    }
}
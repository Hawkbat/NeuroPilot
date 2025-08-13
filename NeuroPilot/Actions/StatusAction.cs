using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;

namespace NeuroPilot.Actions
{
    public class StatusAction : NeuroAction
    {
        public override string Name => "autopilot_status";

        protected override string Description => "Checks the current status of the ship and lists available destinations.";

        protected override JsonSchema Schema => new();

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync()
        {
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                Context.Send("Autopilot is not available because the player is not in-game.");
                return UniTask.CompletedTask;
            }

            var autopilotStatus = EnhancedAutoPilot.GetInstance().GetAutopilotStatus();
            Context.Send(autopilotStatus);

            return UniTask.CompletedTask;
        }
    }
}

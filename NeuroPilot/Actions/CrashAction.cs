using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;

namespace NeuroPilot.Actions
{
    public class CrashAction : NeuroAction<string>
    {
        const string targetPropName = "target";

        public override string Name => "crash_ship";

        protected override string Description => "Crashes the ship directly into the target. Likely destroying the autopilot and killing the player.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [targetPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { targetPropName, QJS.Enum(Destinations.GetRegisteredNames()) },
            },
        };

        protected override ExecutionResult Validate(ActionJData actionData, out string targetName)
        {
            var targetProp = actionData.Data?[targetPropName]?.ToString();
            targetName = targetProp ?? string.Empty;

            if (string.IsNullOrEmpty(targetProp))
            {
                return ExecutionResult.Failure($"`{targetPropName}` is required.");
            }

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be used while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryCrash(targetName, out var error))
            {
                return ExecutionResult.Failure(error);
            }

            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(string targetName)
        {
            // It's too late to signal if autopilot failed to activate so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

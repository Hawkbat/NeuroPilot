using Cysharp.Threading.Tasks;
using HarmonyLib;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;
using System.Linq;

namespace NeuroPilot.Actions
{
    public class OrientAction : NeuroAction<string>
    {
        const string targetPropName = "target";

        public override string Name => "ship_look_at";

        protected override string Description => "Face the ship towards a given object. Or one of those extra bright stars. Cannot be used while landed.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [targetPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { targetPropName, QJS.Enum(Destinations.GetAllValidNames().Append("Exploding Star")) },
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

            if (!EnhancedAutoPilot.GetInstance().TryOrient(targetName, out var error))
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

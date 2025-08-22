
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroPilot.Actions
{
    public class IncreasedFrightsAction : NeuroAction
    {
        public override string Name => "set_increased_frights_mode";

        protected override string Description => "Turn Increased Frights Mode on or off.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = ["on"],
            Properties = new Dictionary<string, JsonSchema>
            {
                ["on"] = new JsonSchema { Type = JsonSchemaType.Boolean },
            }
        };

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            if (actionData.Data?["on"].ToObject<bool>() ?? true) {
                return ExecutionResult.Success("Enabled Increased Frights Mode.");
            }
            else {
                return ExecutionResult.Success("Disabled Increased Frights Mode.");

            }

        }

        protected override async UniTask ExecuteAsync()
        {
            await UniTask.CompletedTask;
        }
    }
}
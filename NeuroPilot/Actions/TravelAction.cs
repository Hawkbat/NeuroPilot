﻿using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;

namespace NeuroPilot.Actions
{
    public class TravelAction : NeuroAction<string>
    {
        const string destinationPropName = "destination";

        public override string Name => "initiate_travel";

        protected override string Description => "Starts the process of taking off in the ship and flying to a specific destination in the solar system.";

        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = [destinationPropName],
            Properties = new Dictionary<string, JsonSchema> {
                { destinationPropName, QJS.Enum(Destinations.GetAllValidNames()) },
            },
        };

        protected override ExecutionResult Validate(ActionJData actionData, out string destinationName)
        {
            var destinationProp = actionData.Data?[destinationPropName]?.ToString();
            destinationName = destinationProp ?? string.Empty;

            if (string.IsNullOrEmpty(destinationProp))
            {
                return ExecutionResult.Failure($"`{destinationPropName}` is required.");
            }

            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
            {
                return ExecutionResult.Failure("Autopilot can only be engaged while in-game.");
            }

            if (!EnhancedAutoPilot.GetInstance().TryEngageTravel(destinationName, out var error))
            {
                return ExecutionResult.Failure(error);
            }


            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(string parsedData)
        {
            // It's too late to signal if autopilot failed to engage so we just execute it in Validate instead. Technically incorrect but reduces latency.
            return UniTask.CompletedTask;
        }
    }
}

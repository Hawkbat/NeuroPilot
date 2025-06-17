using System;
using System.Reflection;
using HarmonyLib;
using NeuroPilot.Actions;
using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace NeuroPilot
{
    public class NeuroPilot : ModBehaviour
    {
        internal static NeuroPilot Instance;

        INeuroAction[] neuroActions;

        protected void Awake()
        {
            Instance = this;
        }

        protected void Start()
        {
            ModHelper.Console.WriteLine($"{nameof(NeuroPilot)} is loaded!", MessageType.Success);

            new Harmony("Hawkbar.NeuroPilot").PatchAll(Assembly.GetExecutingAssembly());

            Environment.SetEnvironmentVariable("NEURO_SDK_WS_URL", "ws://localhost:8678");
            NeuroSdk.NeuroSdkSetup.Initialize("Outer Wilds");

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        protected void OnDestroy()
        {
            CleanUpActions();
        }

        string error;

        protected void OnGUI()
        {
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem) return;
            foreach (var dest in Destinations.GetAll())
            {
                if (GUILayout.Button(dest.name))
                {
                    EnhancedAutoPilot.GetInstance().TryEngageTravel(dest.name, out error);
                }
            }
            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.Label($"<color=red>Error: {error}</color>");
            }
        }

        protected void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem)
            {
                CleanUpActions();
                return;
            }

            Destinations.SetUp();

            EnhancedAutoPilot.GetInstance().OnAutopilotMessage.AddListener(msg =>
            {
                Context.Send(msg);
                var notif = new NotificationData(NotificationTarget.Player, msg);
                NotificationManager.SharedInstance.PostNotification(notif);
            });
            
            SetUpActions();
        }

        protected void SetUpActions()
        {
            if (neuroActions == null)
            {
                neuroActions = [
                    new EngageAutoPilotTravelAction(),
                    new AbortAutoPilotTravelAction(),
                    new CheckAutoPilotAction(),
                ];

                NeuroActionHandler.RegisterActions(neuroActions);
                ModHelper.Console.WriteLine($"Registered {neuroActions.Length} neuro actions.");
            }
        }

        protected void CleanUpActions()
        {
            if (neuroActions != null)
            {
                NeuroActionHandler.UnregisterActions(neuroActions);
                ModHelper.Console.WriteLine($"Unregistered {neuroActions.Length} neuro actions.");
                neuroActions = null;
            }
        }
    }
}

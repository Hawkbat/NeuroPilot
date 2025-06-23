using System;
using System.Collections.Generic;
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
        bool debugMode;

        protected void Awake()
        {
            Instance = this;
        }

        protected void Start()
        {
            ModHelper.Console.WriteLine($"{nameof(NeuroPilot)} is loaded!", MessageType.Success);

            new Harmony("Hawkbar.NeuroPilot").PatchAll(Assembly.GetExecutingAssembly());

            string neuroApiUrl = ModHelper.Config.GetSettingsValue<string>("Neuro API URL");
            if (!string.IsNullOrEmpty(neuroApiUrl))
            {
                Environment.SetEnvironmentVariable("NEURO_SDK_WS_URL", neuroApiUrl);
            }
            NeuroSdk.NeuroSdkSetup.Initialize("Outer Wilds");

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEURO_SDK_WS_URL")))
            {
                ModHelper.MenuHelper.PopupMenuManager.CreateInfoPopup("Neuro API URL was not set. Either set the NEURO_SDK_WS_URL environment variable or set the Neuro API URL in the mod settings.", "OK").Activate();
            }

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public override void Configure(IModConfig config)
        {
            debugMode = config.GetSettingsValue<bool>("Debug Mode");
        }

        protected void OnDestroy()
        {
            CleanUpActions();
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
                logs.Add(msg);
                Context.Send(msg);
            });

            SetUpActions();
        }

        void SetUpActions()
        {
            if (neuroActions == null)
            {
                neuroActions = [
                    new TravelAction(),
                    new TakeOffAction(),
                    new LandAction(),
                    new EvadeAction(),
                    new AbortAutoPilotAction(),
                    new CheckAutoPilotAction(),
                    new SetShipHeadlightsAction(),
                ];

                NeuroActionHandler.RegisterActions(neuroActions);
                ModHelper.Console.WriteLine($"Registered {neuroActions.Length} neuro actions.");
            }
        }

        void CleanUpActions()
        {
            if (neuroActions != null)
            {
                NeuroActionHandler.UnregisterActions(neuroActions);
                ModHelper.Console.WriteLine($"Unregistered {neuroActions.Length} neuro actions.");
                neuroActions = null;
            }
        }

        string error;
        readonly List<string> logs = [];

        protected void OnGUI()
        {
            if (!debugMode) return;
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem) return;
            if (!EnhancedAutoPilot.GetInstance()) return;

            var autopilot = EnhancedAutoPilot.GetInstance();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            GUILayout.Space(20f);

            GUILayout.Label($"Player Location: {Destinations.GetPlayerLocation()?.GetName() ?? "Outer Space"}");
            GUILayout.Label($"Ship Location: {Destinations.GetShipLocation()?.GetName() ?? "Outer Space"}");
            GUILayout.Label($"Landing Velocity: {autopilot.GetCurrentLandingVelocity():F3} / {autopilot.GetTargetLandingVelocity():F3}");

            GUILayout.Label("Task Queue:");
            foreach (var task in autopilot.GetQueuedTasks())
            {
                GUILayout.Label(task.ToString());
            }

            GUILayout.Space(20f);

            GUI.enabled = autopilot.GetCurrentLocationReferenceFrame() != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Take Off") && autopilot.TryTakeOff(out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Land") && autopilot.TryLand(out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.enabled = autopilot.IsAutopilotActive();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Abort") && autopilot.TryAbortTravel(out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(20f);

            foreach (var dest in Destinations.GetAll())
            {
                GUILayout.BeginHorizontal();
                if (!dest.IsAvailable(out string reason)) GUI.enabled = false;
                if (GUILayout.Button("Evade"))
                {
                    autopilot.TryEvade(dest.ToString(), out error);
                }
                if (GUILayout.Button("Travel"))
                {
                    autopilot.TryEngageTravel(dest.ToString(), out error);
                }
                GUILayout.Label(dest.ToString());
                if (!string.IsNullOrEmpty(reason))
                {
                    GUILayout.Label(reason);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }
            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.Label($"<color=red>Error: {error}</color>");
            }

            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            GUILayout.Space(20f);
            GUILayout.Label("Logs:");
            for (var i = Mathf.Max(0, logs.Count - 10); i < logs.Count; i++)
            {
                GUILayout.Label(logs[i]);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}

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
        internal static NeuroPilot instance;

        public static bool ManualOverride => instance.manualOverride;
        public static bool AllowDestructive => instance.allowDestrucive;

        INeuroAction[] neuroActions;
        bool debugMode;
        bool manualOverride;
        bool allowDestrucive;

        protected void Awake()
        {
            instance = this;
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
                ModHelper.Console.WriteLine($"Neuro API URL was not set. Either set the NEURO_SDK_WS_URL environment variable or set the Neuro API URL in the mod settings.", MessageType.Error);
                //ModHelper.MenuHelper.PopupMenuManager.CreateInfoPopup("Neuro API URL was not set. Either set the NEURO_SDK_WS_URL environment variable or set the Neuro API URL in the mod settings.", "OK").Activate();
            }

            Context.Send("Once the player enters the ship for the first time, you will have full control for the rest of the loop unless the ship is destroyed. Be sure to take advantage of your commands. Experiment and have fun!");

            GlobalMessenger.AddListener("EnterShip", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("Player has entered the ship", true));
            GlobalMessenger.AddListener("ExitShip", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("Player has exited the ship", true));
            GlobalMessenger.AddListener("ShipSystemFailure", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("The ship has been destroyed", false));

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        protected void Update()
        {
            IsStrangerNewlyAvailable();
            var autopilotAvailable = LoadManager.GetCurrentScene() == OWScene.SolarSystem && EnhancedAutoPilot.GetInstance() != null && EnhancedAutoPilot.GetInstance().IsAutopilotAvailable();

            if (autopilotAvailable) SetUpActions();
            else CleanUpActions();
        }

        Transform mapSatellite = GameObject.Find("HearthianMapSatellite_Body")?.transform;

        public void IsStrangerNewlyAvailable()
        {
            if (!Destinations.GetByType<StrangerDestination>().GetName().Equals("Nothing"))
                return;

            var ringWorld = Locator.GetAstroObject(AstroObject.Name.RingWorld)?.transform;
            var sun = Locator.GetAstroObject(AstroObject.Name.Sun)?.transform;
            var ship = Locator.GetShipTransform();

            if (!ringWorld || !sun || !ship)
                return;

            if (!mapSatellite) {
                mapSatellite = GameObject.Find("HearthianMapSatellite_Body")?.transform;
                if (mapSatellite == null) return;
            }

            var isNearMapSatellite = Vector3.Distance(ship.position, mapSatellite.position) < 100f;

            if (!isNearMapSatellite)
                return;

            var eclipseDot = Vector3.Dot((sun.position - ringWorld.position).normalized, (ringWorld.position - ship.position).normalized);
            var isEclipseVisible = eclipseDot > 0.97f;

            if (!isEclipseVisible)
                return;

            CleanUpActions();
            SetUpActions();
            EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("A \"Dark shadow over the sun\" has appeared. You should probably travel to it!", false);
        }

        public override void Configure(IModConfig config)
        {
            debugMode = config.GetSettingsValue<bool>("Debug Mode");
            manualOverride = config.GetSettingsValue<bool>("Manual Override");
            allowDestrucive = !config.GetSettingsValue<bool>("Prevent Destructive Actions");
            if (!allowDestrucive) 
            {
                HatchController hatchController = Locator._shipTransform.GetComponentInChildren<HatchController>();
                if (hatchController._hatchObject.activeSelf && !PlayerState.IsInsideShip()) 
                {
                    FindObjectOfType<ShipTractorBeamSwitch>().ActivateTractorBeam();
                    hatchController.OpenHatch();
                }
            }
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

            var probeBody = GameObject.Find("NomaiProbe_Body");
            if (probeBody != null) AddReferenceFrame(probeBody, 300, 5, 15000f);
            else
            {
                ModHelper.Console.WriteLine("NomaiProbe_Body not found!", MessageType.Error);
            }

            Destinations.SetUp();

            var autopilot = EnhancedAutoPilot.GetInstance();

            var anglerFishExhibit = GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Sector_Observatory/Interactables_Observatory/AnglerFishExhibit/InteractVolume");
            if (anglerFishExhibit != null)
            {
                var interaction = anglerFishExhibit.GetComponent<InteractVolume>();
                if (interaction != null)
                {
                    ((SingleInteractionVolume)interaction).OnPressInteract += () => autopilot.OnAutopilotMessage.Invoke("Oh look its Ernesto the anglerfish!", false);
                }
            }

            var ship = GameObject.Find("Ship_Body");
            var listener = ship.GetComponent<ShipDestroyListener>();
            if (listener == null)
            {
                listener = ship.AddComponent<ShipDestroyListener>();
            }

            autopilot.OnAutopilotMessage.AddListener((msg, silent) =>
            {
                logs.Add(msg);
                Context.Send(msg, silent);
            });
        }

        public static ReferenceFrameVolume AddReferenceFrame(GameObject obj, float radius, float minTargetRadius, float maxTargetRadius)
        {
            obj.GetAttachedOWRigidbody().SetIsTargetable(false);
            var go = new GameObject("RFVolume");
            go.transform.parent = obj.transform;
            go.transform.localPosition = Vector3.zero;
            go.layer = LayerMask.NameToLayer("ReferenceFrameVolume");
            go.SetActive(false);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var rf = new ReferenceFrame(obj.GetAttachedOWRigidbody())
            {
                _minSuitTargetDistance = minTargetRadius,
                _maxTargetDistance = maxTargetRadius,
                _autopilotArrivalDistance = radius,
                _autoAlignmentDistance = radius * 0.75f,
                _hideLandingModePrompt = false,
                _matchAngularVelocity = true,
                _minMatchAngularVelocityDistance = 70,
                _maxMatchAngularVelocityDistance = 400,
                _bracketsRadius = radius * 0.5f
            };

            var rfv = go.AddComponent<ReferenceFrameVolume>();
            rfv._referenceFrame = rf;
            rfv._minColliderRadius = minTargetRadius;
            rfv._maxColliderRadius = radius;
            rfv._isPrimaryVolume = false;
            rfv._isCloseRangeVolume = false;

            rf._useCenterOfMass = false;
            rf._localPosition = Vector3.zero;
            go.transform.localPosition = Vector3.zero;

            go.SetActive(true);

            return rfv;
        }

        void SetUpActions()
        {
            if (neuroActions == null)
            {
                Destinations.UpdateNames();
                neuroActions = [
                    new TravelAction(),
                    new TakeOffAction(),
                    new LandAction(),
                    new EvadeAction(),
                    new AbortAutoPilotAction(),
                    new StatusAction(),
                    new ControlShipHatchAction(),
                    new ControlShipHeadlightsAction(),
                    new SpinAction(),
                    new EjectAction(),
                    new CrashAction(),
                ];

                NeuroActionHandler.RegisterActions(neuroActions);
                Context.Send("Autopilot control is now available.");
                ModHelper.Console.WriteLine($"Registered {neuroActions.Length} neuro actions.");
            }
        }

        public void CleanUpActions()
        {
            if (neuroActions != null)
            {
                NeuroActionHandler.UnregisterActions(neuroActions);
                if ((EnhancedAutoPilot.GetInstance()?.IsAutopilotDamaged() ?? true) && TimeLoop.GetSecondsRemaining() > 0)
                    Context.Send("There is a problem with your AI. Autopilot control is not available.");
                else
                    Context.Send("Autopilot control is temporarily unavailable.");
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

            GUILayout.Label($"Neuro Actions Active: {neuroActions != null}");

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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Hatch") && autopilot.TryControlHatch(true, out error)) { }
            if (GUILayout.Button("Close Hatch") && autopilot.TryControlHatch(false, out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Headlights On") && autopilot.TryControlHeadlights(true, out error)) { }
            if (GUILayout.Button("Headlights Off") && autopilot.TryControlHeadlights(false, out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spin") && autopilot.Spin(out error)) { }
            if (GUILayout.Button("Eject") && autopilot.Eject(out error)) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

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
                if (GUILayout.Button("Crash"))
                {
                    autopilot.TryCrash(dest.ToString(), out error);
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

    public class ShipDestroyListener : MonoBehaviour
    {

        void OnDisable()
        {
            var autopilot = EnhancedAutoPilot.GetInstance();
            autopilot.TryAbortTravel(out _);
            autopilot.OnAutopilotMessage.Invoke("The ship has been destroyed", false);
            NotificationManager.SharedInstance.PostNotification(new NotificationData(NotificationTarget.All, $"Connection with ship is lost".ToUpper()));
        }
    }
}

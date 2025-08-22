using HarmonyLib;
using NeuroPilot.Actions;
using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NeuroPilot
{
    public class NeuroPilot : ModBehaviour //TODO ship light colors.. should Bloom? //TODO  //TODO instruments while not in pilots seat //TODO on abort, cockpitController._thrustController._shipAlignment.enabled = landing mode enabled //TODO unkown destination isnt working, i use brittle chunks at the WH to check //TODO targeted destination changing mid flight messes with obstacles //TODO sometimes cant target //TODO better prompts and completion alerts //TODO replace nulls with ?? or if(gameObject) //TODO check ?.'s //TODO sort all methods 
    {
        internal static NeuroPilot instance;

        INeuroAction[] autopilotActions;
        INeuroAction[] strangerActions;

        float changeCheckCooldown;
        bool strangerDiscovered;

        public bool IsAutopilotAvailable =>
            LoadManager.GetCurrentScene() == OWScene.SolarSystem && EnhancedAutoPilot.GetInstance() && EnhancedAutoPilot.GetInstance().IsAutopilotAvailable();

        protected void Awake()
        {
            instance = this;
        }

        protected void Start()
        {
            ModHelper.Console.WriteLine($"{nameof(NeuroPilot)} is loaded!", MessageType.Success);

            new Harmony("Hawkbar.NeuroPilot").PatchAll(Assembly.GetExecutingAssembly());

            string neuroApiUrl = ModConfig.NeuroApiUrl;
            if (!string.IsNullOrEmpty(neuroApiUrl))
                Environment.SetEnvironmentVariable("NEURO_SDK_WS_URL", neuroApiUrl);

            NeuroSdk.NeuroSdkSetup.Initialize("Outer Wilds");

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEURO_SDK_WS_URL")))
                ModHelper.Console.WriteLine($"Neuro API URL was not set. Either set the NEURO_SDK_WS_URL environment variable or set the Neuro API URL in the mod settings.", MessageType.Error);

            Context.Send("Once the player enters the ship for the first time, you will have full control for the rest of the loop unless the ship is destroyed. Be sure to take advantage of your commands. Experiment and have fun!");

            RegisterListeners();

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private void RegisterListeners()
        {
            GlobalMessenger.AddListener("EnterShip", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("Player has entered the ship", true));
            GlobalMessenger.AddListener("ExitShip", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("Player has exited the ship", true));
            GlobalMessenger.AddListener("ShipSystemFailure", () => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("The ship has been destroyed", false));
            GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", _ => EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke($"{Destinations.GetByType<TargetedDestination>().GetDestinationName()} was targeted", true));
        }

        protected void OnDestroy()
        {
            CleanUpAutopilotActions();
            CleanUpScopeActions();
            CleanUpStrangerActions();
        }

        protected void Update()
        {
            IsStrangerNewlyAvailable();

            if (IsAutopilotAvailable) SetUpAutopilotActions();
            else CleanUpAutopilotActions();

            if (changeCheckCooldown <= 0f)
            {
                if (Destinations.CheckForChanges())
                {
                    UpdateAutopilotActions();
                    changeCheckCooldown = 1f;
                }
                else
                {
                    changeCheckCooldown = 1f / 30f;
                }
            }
            else
            {
                changeCheckCooldown -= Time.deltaTime;
            }
        }

        public override void Configure(IModConfig config)
        {
            if (ModConfig.AllowDestructive)
                return;

            HatchController hatchController = Locator._shipTransform?.GetComponentInChildren<HatchController>();
            if (hatchController && hatchController._hatchObject.activeSelf && !PlayerState.IsInsideShip())
            {
                FindObjectOfType<ShipTractorBeamSwitch>().ActivateTractorBeam();
                hatchController.OpenHatch();
            }
        }

        protected void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem)
            {
                CleanUpAutopilotActions();
                CleanUpStrangerActions();
                CleanUpScopeActions();
                return;
            }

            Destinations.SetUp();

            var autopilot = EnhancedAutoPilot.GetInstance();

            var anglerFishinteraction = GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Sector_Observatory/Interactables_Observatory/AnglerFishExhibit/InteractVolume")?.GetComponent<InteractVolume>();
            if (anglerFishinteraction)
                ((SingleInteractionVolume)anglerFishinteraction).OnPressInteract += () => autopilot.OnAutopilotMessage.Invoke("Oh look its Ernesto the anglerfish!", false);

            var ship = GameObject.Find("Ship_Body").gameObject;
            ship.GetAddComponent<ShipDestroyListener>();

            autopilot.OnAutopilotMessage.AddListener((msg, silent) =>
            {
                logs.Add(msg);
                Context.Send(msg, silent);
            });

            SetUpScopeActions();
        }

        public void IsStrangerNewlyAvailable() //TODO allow without at sattelite? //TODO dont use strangerDiscovered
        {
            if (!Destinations.GetByType<StrangerDestination>().IsAvailable(out _))
                return;

            if (strangerDiscovered)
                return;

            var ringWorld = Locator._ringWorld?.transform;
            var sun = Locator.GetSunTransform();
            var ship = Locator.GetShipTransform();

            var mapSatellite = Locations.GetMapSatellite();
            var isNearMapSatellite = Vector3.Distance(ship.position, mapSatellite.position) < 200f;

            if (!isNearMapSatellite)
                return;

            var eclipseDot = Vector3.Dot((sun.position - ringWorld.position).normalized, (ringWorld.position - ship.position).normalized);
            var isEclipseVisible = eclipseDot > 0.97f;

            if (!isEclipseVisible)
                return;

            UpdateAutopilotActions();
            EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke("A \"Dark shadow over the sun\" has appeared. You should probably travel to it!", false);
            strangerDiscovered = true;
        }

        public void SetUpAutopilotActions(bool silent = false)
        {
            if (autopilotActions == null)
            {
                autopilotActions = [ //TODO only register available ones
                    new TravelAction(),
                    new TakeOffAction(),
                    new LandAction(),
                    new EvadeAction(),
                    new AbortAutoPilotAction(),
                    new GetDestinationLocationsAction(),
                    new StatusAction(),
                    new ControlShipHatchAction(),
                    new ControlShipHeadlightsAction(),
                    new SpinAction(),
                    new EjectAction(),
                    new CrashAction(),
                    new OrientAction(),
                ];

                NeuroActionHandler.RegisterActions(autopilotActions);

                if (!silent)
                {
                    Context.Send("Autopilot control is now available. You can use actions to control Vedal's ship in Outer Wilds. You may perform any action you wish, at any time, even unprompted.");
                    ModHelper.Console.WriteLine($"Registered {autopilotActions.Length} neuro actions.");
                }
            }
        }

        public void SetUpStrangerActions(bool silent = false)
        {
            if (strangerActions == null)
            {
                strangerActions = [ //TODO only register available ones
                    new IncreasedFrightsAction(),
                ];

                NeuroActionHandler.RegisterActions(strangerActions);

                if (!silent)
                {
                    Context.Send("Something is interfering with your ability to control the ship, but maybe you can control something else...");
                    ModHelper.Console.WriteLine($"Registered {strangerActions.Length} neuro actions.");
                }
            }
        }

        public void CleanUpAutopilotActions(bool silent = false)
        {
            strangerDiscovered = false;

            if (autopilotActions != null)
            {
                NeuroActionHandler.UnregisterActions(autopilotActions);

                if (!silent)
                {
                    if ((!EnhancedAutoPilot.GetInstance() || EnhancedAutoPilot.GetInstance().IsAutopilotDamaged()) && TimeLoop.GetSecondsRemaining() > 0)
                        Context.Send("There is a problem with your AI. Autopilot control is not available.");
                    else
                        Context.Send("Autopilot control is temporarily unavailable.");
                    ModHelper.Console.WriteLine($"Unregistered {autopilotActions.Length} neuro actions.");
                }
                autopilotActions = null;
            }

        }

        public void CleanUpStrangerActions(bool silent = false)
        {
            strangerDiscovered = false;

            if (strangerActions != null)
            {
                NeuroActionHandler.UnregisterActions(strangerActions);

    
                strangerActions = null;
            }

        }

        public void UpdateAutopilotActions()
        {
            CleanUpAutopilotActions(true);
            if (IsAutopilotAvailable)
            {
                SetUpAutopilotActions(true);
            }
            if (Locator.GetCloakFieldController().isShipInsideCloak) {
                SetUpStrangerActions();
            }
        }

        public void SetUpScopeActions()
        {
            NeuroActionHandler.RegisterActions(new PlayerStatusAction(), new ShipStatusAction());
        }

        public void CleanUpScopeActions()
        {
            NeuroActionHandler.UnregisterActions("player_status", "ship_status", "launch_scout", "take_scout_photo", "retrieve_scout", "spin_scout", "turn_scout_camera");
        }

        string error;
        readonly List<string> logs = [];
        Destination selectedDestination;

        protected void OnGUI()
        {
            if (!ModConfig.DebugMode) return;
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem) return;
            if (!EnhancedAutoPilot.GetInstance()) return;

            var autopilot = EnhancedAutoPilot.GetInstance();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            GUILayout.Space(20f);

            GUILayout.Label($"Neuro Actions Active: {autopilotActions != null}");

            GUILayout.Label($"Player Location: {Destinations.GetPlayerLocation()?.Name ?? "Outer Space"}");
            GUILayout.Label($"Ship Location: {Destinations.GetShipLocation()?.Name ?? "Outer Space"}");
            GUILayout.Label($"Landing Velocity: {autopilot.GetCurrentLandingVelocity():F3} / {autopilot.GetTargetLandingVelocity():F3}");

            GUILayout.Label("Task Queue:");
            foreach (var task in autopilot.GetQueuedTasks())
            {
                GUILayout.Label(task.ToString());
            }

            if (Locator._pauseCommandListener._pauseMenu && Locator._pauseCommandListener._pauseMenu.IsOpen())
            {
                GUILayout.Space(20f);

                GUI.enabled = autopilot.GetCurrentLocation() != null;
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
                if (GUILayout.Button("Look Nova") && autopilot.TryOrient("Exploding Star", out error)) { }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (selectedDestination != null)
                {
                    GUILayout.Label($"Selected: {selectedDestination}");
                    if (!selectedDestination.IsAvailable(out _)) GUI.enabled = false;
                    if (GUILayout.Button("Evade"))
                    {
                        autopilot.TryEvade(selectedDestination.Name, out error);
                    }
                    if (GUILayout.Button("Travel"))
                    {
                        autopilot.TryEngageTravel(selectedDestination.Name, out error);
                    }
                    if (GUILayout.Button("Crash"))
                    {
                        autopilot.TryCrash(selectedDestination.Name, out error);
                    }
                    if (GUILayout.Button("Look"))
                    {
                        autopilot.TryOrient(selectedDestination.Name, out error);
                    }
                    foreach (var (locationName, _) in Locations.ByDestination(selectedDestination))
                    {
                        if (GUILayout.Button(locationName))
                        {
                            autopilot.TryOrbitToLocation(selectedDestination.Name, locationName, out error);
                        }
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndVertical();
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("None"))
                {
                    selectedDestination = null;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                foreach (var dest in Destinations.GetAll())
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled = dest.IsAvailable(out string reason) && dest != selectedDestination;
                    if (GUILayout.Button(dest.Name))
                    {
                        selectedDestination = dest;
                    }
                    if (!string.IsNullOrEmpty(reason))
                    {
                        GUILayout.Label(reason);
                    }
                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
            }

            GUILayout.Space(20f);
            GUILayout.Label("Logs:");
            for (var i = Mathf.Max(0, logs.Count - 10); i < logs.Count; i++)
            {
                GUILayout.Label(logs[i]);
            }
            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.Label($"<color=red>Error: {error}</color>");
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}

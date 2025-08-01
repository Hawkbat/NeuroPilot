using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace NeuroPilot
{
    public class EnhancedAutoPilot : MonoBehaviour
    {
        public class AutopilotEvent : UnityEvent<string, bool> {}

        private const float STUCK_TIMEOUT = 5f;
        private const float TAKEOFF_TARGET_VELOCITY = 300f;
        private const float LANDING_TARGET_VELOCITY = -30f;

        private static EnhancedAutoPilot instance;

        public AutopilotEvent OnAutopilotMessage = new();

        ShipCockpitController cockpitController;
        Autopilot autopilot;
        SectorDetector shipSectorDetector;
        HatchController hatchController;

        readonly Queue<AutoPilotTask> taskQueue = new();
        NotificationData taskNotification;
        bool playerHasEnteredShip;
        float stuckTime;
        readonly List<Destination> possibleObstacles = [];
        readonly List<Destination> activeObstacles = [];

        public static EnhancedAutoPilot GetInstance()
        {
            if (instance) return instance;
            var shipCockpitController = FindObjectOfType<ShipCockpitController>();
            if (!shipCockpitController) return null;
            instance = shipCockpitController.gameObject.GetAddComponent<EnhancedAutoPilot>();
            return instance;
        }

        public Destination GetCurrentDestination() =>
            (GetCurrentTask() is TravelTask travelTask ? travelTask.destination : null) ?? Destinations.GetByReferenceFrame(autopilot._referenceFrame);

        public string GetCurrentDestinationName() =>
            GetCurrentDestination()?.ToString() ?? autopilot._referenceFrame?.GetHUDDisplayName() ?? string.Empty;

        public Destination GetCurrentLocation() => Destinations.GetShipLocation() ?? Destinations.GetByReferenceFrame(shipSectorDetector.GetPassiveReferenceFrame());

        public ReferenceFrame GetCurrentLocationReferenceFrame() => Destinations.GetShipLocation()?.GetReferenceFrame() ??
            shipSectorDetector.GetPassiveReferenceFrame();

        public ReferenceFrame GetLandingReferenceFrame() => GetCurrentTask() is TakeOffTask takeOffTask ? takeOffTask.location : GetCurrentTask() is LandingTask landingTask ? landingTask.location : null;

        public AutoPilotTask GetCurrentTask() => taskQueue.TryPeek(out var task) ? task : null;
        public IEnumerable<AutoPilotTask> GetQueuedTasks() => taskQueue;

        public bool IsManualAllowed() => !IsAutopilotActive() && 
            (NeuroPilot.ManualOverride
            || GetCurrentLocation()?.GetDistanceToShip() < GetCurrentLocation()?.GetReferenceFrame()?.GetAutopilotArrivalDistance() + 100f
            || GetCurrentLocationReferenceFrame() != null
            || EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned && Locator.GetCloakFieldController().isShipInsideCloak);

        public bool IsAutopilotActive() => GetCurrentTask() != null;
        public bool IsTraveling() => GetCurrentTask() is TravelTask;
        public bool IsTakingOff() => GetCurrentTask() is TakeOffTask;
        public bool IsLanding() => GetCurrentTask() is LandingTask;
        public bool IsEvading() => GetCurrentTask() is EvadeTask;

        public IEnumerable<Destination> GetPossibleObstacles() => possibleObstacles;
        public IEnumerable<Destination> GetActiveObstacles() => activeObstacles;

        public bool IsAutopilotAvailable() => playerHasEnteredShip && !IsAutopilotDamaged() && !(NeuroPilot.ManualOverride && PlayerState.AtFlightConsole());
        public bool IsAutopilotDamaged() => autopilot.IsDamaged() || cockpitController._shipSystemFailure || !cockpitController._shipBody.gameObject.activeSelf;

        protected void Awake()
        {
            cockpitController = gameObject.GetComponent<ShipCockpitController>();
            autopilot = cockpitController._autopilot;
            shipSectorDetector = transform.root.GetComponentInChildren<SectorDetector>();
            hatchController = transform.root.GetComponentInChildren<HatchController>();
            autopilot.OnArriveAtDestination += Autopilot_OnArriveAtDestination;
            autopilot.OnAlreadyAtDestination += Autopilot_OnAlreadyAtDestination;
            autopilot.OnAbortAutopilot += Autopilot_OnAbortAutopilot;

            GlobalMessenger.AddListener("EnterShip", OnEnterShip);
        }

        protected void OnDestroy()
        {
            autopilot.OnArriveAtDestination -= Autopilot_OnArriveAtDestination;
            autopilot.OnAlreadyAtDestination -= Autopilot_OnAlreadyAtDestination;
            autopilot.OnAbortAutopilot -= Autopilot_OnAbortAutopilot;

            GlobalMessenger.RemoveListener("EnterShip", OnEnterShip);
        }

        protected void Update()
        {
            var currentTask = GetCurrentTask();
            if (currentTask is TakeOffTask or LandingTask)
            {
                if (!cockpitController.InLandingMode())
                {
                    cockpitController.EnterLandingMode();
                }

                var isStuck = Math.Abs(GetCurrentLandingVelocity()) < 2f;
                if (isStuck)
                {
                    stuckTime += Time.deltaTime;
                    if (stuckTime > STUCK_TIMEOUT)
                    {
                        stuckTime = 0;

                        if (currentTask is TakeOffTask)
                        {
                            OnAutopilotMessage.Invoke($"Autopilot has aborted because the ship became stuck while trying to take off.", false);
                            AbortTask();
                        }
                        else
                        {
                            OnAutopilotMessage.Invoke($"Autopilot complete. The ship has come to a stop but may not be on solid ground.", false);
                            CompleteTask();
                        }
                    }
                }
                else
                {
                    stuckTime = 0f;
                }
            }
            if (currentTask is TakeOffTask takeOffTask)
            {
                if ((takeOffTask.location.GetPosition() - cockpitController._shipBody.GetWorldCenterOfMass()).magnitude > takeOffTask.location.GetAutopilotArrivalDistance() + 100f )
                {
                    OnAutopilotMessage.Invoke("Autopilot has successfully taken off from the current location.", true);
                    CompleteTask();
                }
            }
            if (currentTask is LandingTask landTask)
            {
                if (cockpitController._landingManager.IsLanded())
                {
                    OnAutopilotMessage.Invoke("Autopilot has successfully landed at the current location.", false);
                    CompleteTask();
                }
            }
            if (currentTask is EvadeTask evadeTask)
            {
                var target = evadeTask.location;
                if (target != null)
                {
                    var towardsDir = (target.GetPosition() - Locator.GetShipBody().GetPosition()).normalized;
                    var relativeVelocity = Locator.GetShipBody().GetRelativeVelocity(target).normalized;
                    var isMovingAway = Vector3.Dot(towardsDir, relativeVelocity) > 0;
                    if (isMovingAway)
                    {
                        OnAutopilotMessage.Invoke($"Autopilot successfully evaded {Destinations.GetByReferenceFrame(evadeTask.location)?.GetName() ?? evadeTask.location.GetHUDDisplayName()}.", false);
                        CompleteTask();
                    }
                }
                else
                {
                    OnAutopilotMessage.Invoke("Autopilot has aborted evasion because the target location no longer exists.", false);
                    AbortTask();
                }
            }
            if (currentTask is TravelTask travelTask)
            {
                if (!autopilot.IsFlyingToDestination() && !autopilot.FlyToDestination(travelTask.destination.GetReferenceFrame()))
                {
                    OnAutopilotMessage.Invoke($"Autopilot failed to engage travel to destination '{travelTask.destination}'.", false);
                    AbortTask();
                }
                if (EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned && Locator.GetCloakFieldController().isShipInsideCloak)
                {
                    OnAutopilotMessage.Invoke("Autopilot has aborted travel because the ship has entered a cloaking field.", false);
                    AbortTask();
                }
            }

            UpdateObstacles();

            if (!IsManualAllowed())
            {
                cockpitController._thrustController.enabled = !IsAutopilotDamaged();
                cockpitController._thrustController._shipAlignment.enabled = IsTakingOff() || IsLanding();
                cockpitController._thrustController._shipAlignment._localAlignmentAxis = Vector3.down;
            }
        }

        public float GetCurrentLandingVelocity()
        {
            var rfv = GetLandingReferenceFrame();
            if (rfv != null)
            {
                var upAxis = (Locator.GetShipBody().GetPosition() - rfv.GetPosition()).normalized;
                var relativeVelocity = -Locator.GetShipBody().GetRelativeVelocity(rfv);
                var upVelocity = Vector3.Dot(relativeVelocity, upAxis);
                return upVelocity;
            }
            return 0f;
        }

        public float GetTargetLandingVelocity()
        {
            var currentTask = GetCurrentTask();
            if (currentTask is TakeOffTask) return TAKEOFF_TARGET_VELOCITY;
            if (currentTask is LandingTask) return Math.Min(-(((cockpitController._shipBody.GetPosition() - GetLandingReferenceFrame().GetPosition()).magnitude - Destinations.GetByReferenceFrame(GetLandingReferenceFrame())?.GetInnerRadius() ?? 200f) / 5), LANDING_TARGET_VELOCITY);
            return 0f;
        }

        public bool TryEngageTravel(string destinationName, out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            var destination = Destinations.GetByName(destinationName);
            if (destination == null)
            {
                error = $"Destination '{destinationName}' not found. Valid destinations are: {string.Join(", ", Destinations.GetAllValidNames())}";
                return false;
            }

            if (!destination.IsAvailable(out string validationError))
            {
                error = $"Destination '{destinationName}' is not currently available: {validationError}";
                return false;
            }

            var refFrame = destination.GetReferenceFrame();
            if (refFrame == null)
            {
                error = $"Cannot acquire a lock on destination '{destinationName}'.";
                return false;
            }

            AcceptTask(new TravelTask(destination));
            error = string.Empty;
            return true;
        }

        public bool TryAbortTravel(out string error)
        {
            if (!IsAutopilotActive() && !autopilot.IsFlyingToDestination())
            {
                error = "Autopilot is not currently engaged.";
                return false;
            }

            if (autopilot.IsFlyingToDestination()) autopilot.Abort();
            if (IsAutopilotActive()) AbortTask();

            OnAutopilotMessage.Invoke("Autopilot has been manually aborted.", true);

            error = string.Empty;
            return true;
        }

        public bool TryTakeOff(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (GetCurrentLocationReferenceFrame() == null)
            {
                error = "Cannot take off because the ship is not currently landed at a location.";
                return false;
            }

            AcceptTask(new TakeOffTask(GetCurrentLocationReferenceFrame()));
            error = string.Empty;
            return true;
        }

        public bool TryLand(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (GetCurrentLocationReferenceFrame() == null)
            {
                error = "Cannot land because the ship is not currently in a valid location to land at.";
                return false;
            }
            if (cockpitController._landingManager.IsLanded())
            {
                error = "The ship is already landed. No need to land again.";
                return false;
            }

            AcceptTask(new LandingTask(GetCurrentLocationReferenceFrame()));
            error = string.Empty;
            return true;
        }

        public bool TryEvade(string destinationName, out string error)
        {
            if (IsTraveling()) AbortTask();

            if (!ValidateAutopilotStatus(out error)) return false;

            var destination = Destinations.GetByName(destinationName);
            if (destination == null)
            {
                error = $"Destination '{destinationName}' not found. Valid destinations are: {string.Join(", ", Destinations.GetAllValidNames())}";
                return false;
            }

            if (!destination.IsAvailable(out string validationError))
            {
                error = $"Destination '{destinationName}' is not currently available: {validationError}";
                return false;
            }

            var refFrame = destination.GetReferenceFrame();
            if (refFrame == null)
            {
                error = $"Cannot acquire a lock on destination '{destinationName}'.";
                return false;
            }

            AcceptTask(new EvadeTask(refFrame));
            error = string.Empty;
            return true;
        }

        public bool TryControlHeadlights(bool on, out string error)
        {
            if (IsAutopilotDamaged())
            {
                error = "Cannot control ship headlights while the ship is damaged.";
                return false;
            }

            if (cockpitController._externalLightsOn == on)
            {
                error = $"Ship headlights are already {(on ? "on" : "off")}.";
                return false;
            }

            cockpitController._externalLightsOn = on;
            cockpitController.SetEnableShipLights(on);

            error = string.Empty;
            return true;
        }

        public bool TryControlHatch(bool open, out string error)
        {
            if (IsAutopilotDamaged())
            {
                error = "Cannot control ship hatch while the ship is damaged.";
                return false;
            }
            if (hatchController._hatchObject.activeSelf != open)
            {
                error = $"Ship hatch is already {(open ? "open" : "closed")}.";
                return false;
            }

            if (open)
            {
                if (!PlayerState.IsInsideShip())
                FindObjectOfType<ShipTractorBeamSwitch>().ActivateTractorBeam();
                hatchController.OpenHatch();
            }
            else
            {
                FindObjectOfType<ShipTractorBeamSwitch>().DeactivateTractorBeam();
                hatchController.CloseHatch();
            }

            error = string.Empty;
            return true;
        }

        public string GetAutopilotStatus()
        {
            List<string> messages = new List<string>();

            if (autopilot.IsDamaged())
            {
                return "Autopilot module is damaged. There is a problem with your AI.";
            }
            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
            {
                return "Autopilot is not available. The manual override has been engaged by the pilot.";
            }
            if (!playerHasEnteredShip)
            {
                return "Autopilot is not available. The ship has not been powered on yet.";
            }
            if (cockpitController._shipSystemFailure || !cockpitController._shipBody.gameObject.activeSelf)
            {
                return "The ship has been destroyed.";
            }

            if (autopilot.IsFlyingToDestination())
            {
                messages.Add($"Autopilot is currently engaged to travel to destination: {GetCurrentDestinationName()}.");
            }
            else if (IsLanding())
            {
                messages.Add($"Autopilot is currently landing at: {GetCurrentDestinationName()}.");
            }
            else if(IsTakingOff())
            {
                messages.Add($"Autopilot is currently taking off from: {GetCurrentDestinationName()}.");
            }
            else if(IsEvading())
            {
                messages.Add($"Autopilot is currently evding: {GetCurrentDestinationName()}.");
            }
            else
            { 
                messages.Add($"Autopilot is currently idle. You can engage it to travel to a destination."); 
            }

            messages.Add($"Avalible destinations: [{string.Join(", ", Destinations.GetAllValidNames())}]");
            messages.Add($"Ship hatch is {(hatchController._hatchObject.activeSelf ? "closed" : "open")}");
            messages.Add($"Ship headlights are {(cockpitController._externalLightsOn ? "on" : "off")}.");
            messages.Add($"{Destinations.GetByType<TargetedDestination>().GetDestinationName()} is currently targeted.");

            return string.Join(" \n ", messages);
        }

        float spinTime;

        public bool Spin(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) 
                return false;
            if (!(cockpitController._thrustController._isRotationalThrustEnabled && cockpitController._thrustController._shipAlignment.CheckAlignmentRequirements()))
            {
                error = "Ship has recently touched the ground and cannot spin.";
                return false;
            }

            cockpitController.ExitLandingMode();
            spinTime = 180;

            error = string.Empty;
            return true;
        }

        public bool IsSpinning()
        {
            return spinTime > 0;
        }

        public void UpdateSpinning()
        {
            if (spinTime > 0)
                spinTime -= 1;
        }

        private bool ValidateAutopilotStatus(out string error)
        {
            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
            {
                error = "Autopilot cannot be engaged while the manual override is active and someone is actively piloting.";
                return false;
            }
            if (!playerHasEnteredShip)
            {
                error = "Autopilot cannot be engaged until the ship has been powered on.";
                return false;
            }
            if (autopilot.IsDamaged())
            {
                error = "Autopilot module is damaged and cannot be engaged until it is repaired manually. There is a problem with your AI.";
                return false;
            }
            if (cockpitController._shipSystemFailure || !cockpitController._shipBody.gameObject.activeSelf)
            {
                error = "The ship has been destroyed.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void UpdateObstacles()
        {
            if (!IsTraveling())
            {
                possibleObstacles.Clear();
                activeObstacles.Clear();
                return;
            }

            var currentDestination = GetCurrentDestination();
            var currentLocation = GetCurrentLocation();
            var start = cockpitController._shipBody.GetPosition();
            var end = currentDestination?.GetReferenceFrame()?.GetPosition() ?? start;

            foreach (var d in Destinations.GetAll())
            {
                if (d is TargetedDestination)
                    continue;
                if (d == currentDestination || d == currentLocation || d.GetReferenceFrame() == null || d is ShipDestination)
                {
                    if (possibleObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is no longer a potential obstacle on the current travel path.", true);
                        possibleObstacles.Remove(d);
                    }
                    if (activeObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is no longer an active obstacle on the current travel path.", true);
                        activeObstacles.Remove(d);
                    }
                    continue;
                }

                Vector3 nearestPoint;
                var pos = d.GetReferenceFrame().GetPosition();
                var distanceToObject = Vector3.Distance(pos, start);
                if (Vector3.Dot(end - start, pos - start) <= 0f)
                {
                    nearestPoint = start;
                }
                else if (Vector3.Dot(start - end, pos - end) <= 0f)
                {
                    nearestPoint = end;
                }
                else
                {
                    var dir = (end - start).normalized;
                    var t = Vector3.Dot(pos - start, dir);
                    nearestPoint = start + t * dir;
                }

                var distanceToPath = Vector3.Distance(nearestPoint, pos);
                if (distanceToPath < d.GetInnerRadius())
                {
                    if (possibleObstacles.Contains(d))
                    {
                        possibleObstacles.Remove(d);
                    }
                    if (!activeObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is now an active obstacle on the current travel path, {distanceToObject:F2} meters away.", false);
                        activeObstacles.Add(d);
                    }
                }
                else if (distanceToPath < d.GetOuterRadius())
                {
                    if (activeObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is no longer an active obstacle on the current travel path.", true);
                        activeObstacles.Remove(d);
                    }
                    if (!possibleObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is a potential obstacle on the current travel path, {distanceToObject:F2} meters away.", true);
                        possibleObstacles.Add(d);
                    }
                }
                else
                {
                    if (possibleObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is no longer a potential obstacle on the current travel path.", true);
                        possibleObstacles.Remove(d);
                    }
                    if (activeObstacles.Contains(d))
                    {
                        OnAutopilotMessage.Invoke($"{d} is no longer an active obstacle on the current travel path.", true);
                        activeObstacles.Remove(d);
                    }
                }
            }
        }

        private void AcceptTask(AutoPilotTask task, bool silent = false)
        {
            if (taskQueue.Count > 0) AbortTask();
            if (task is TravelTask travelTask)
            {
                if ((Destinations.GetShipLocation()?.CanLand() ?? false) || cockpitController._landingManager.IsLanded())
                {
                    taskQueue.Enqueue(new TakeOffTask(GetCurrentLocationReferenceFrame()));
                }
                taskQueue.Enqueue(task);
                if (travelTask.destination.CanLand())
                {
                    taskQueue.Enqueue(new LandingTask(travelTask.destination.GetReferenceFrame()));
                }
            }
            else
            {
                taskQueue.Enqueue(task);
            }
            RunTask();
        }

        private void RunTask(bool silent = false)
        {
            if (taskQueue.Count > 0)
            {
                OnAutopilotMessage.Invoke($"Autopilot engaged to {GetCurrentTask().ToString()}", true);
                taskNotification = new NotificationData(NotificationTarget.All, $"Autopilot Engaged: {GetCurrentTask()}".ToUpper());
                NotificationManager.SharedInstance.PostNotification(taskNotification, true);
            }
        }

        bool autopilotAbortDebounce;

        private void AbortTask(bool silent = false)
        {
            if (!autopilotAbortDebounce && autopilot.IsFlyingToDestination())
            {
                autopilotAbortDebounce = true;
                autopilot.Abort();
                autopilotAbortDebounce = false;
            }
            else {
                cockpitController._shipAudioController.PlayAutopilotOff();
            }

            if (IsTakingOff() || IsLanding())
            {
                cockpitController.ExitLandingMode();
            }
            taskQueue.Clear();
            if (taskNotification != null)
            {
                NotificationManager.SharedInstance.UnpinNotification(taskNotification);
                taskNotification = null;
            }
        }

        private void CompleteTask()
        {
            if (taskNotification != null)
            {
                NotificationManager.SharedInstance.UnpinNotification(taskNotification);
                taskNotification = null;
            }
            if (IsTakingOff() || IsLanding())
            {
                cockpitController.ExitLandingMode();
            }
            taskQueue.Dequeue();
            RunTask();
        }

        private void Autopilot_OnArriveAtDestination(float arrivalError)
        {
            if (arrivalError > 100f)
            {
                if (GetCurrentDestination() is PlayerDestination && Destinations.GetPlayerLocation() != null)
                {
                    AcceptTask(new LandingTask(Destinations.GetPlayerLocation().GetReferenceFrame()));
                }
                else
                {
                    // We effectively *didn't* arrive at the destination if the error is too large; retry
                    AcceptTask(new TravelTask(GetCurrentDestination()), true);
                }
                return;
            }
            else if (arrivalError > 50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (undershot by {arrivalError:F2} meters).", true);
            }
            else if (arrivalError < -50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (overshot by {Mathf.Abs(arrivalError):F2} meters).", true);
            }
            else
            {
                OnAutopilotMessage.Invoke($"Autopilot successfully arrived at destination: {GetCurrentDestinationName()}.", true);
            }

            if (IsTraveling()) CompleteTask();
        }

        private void Autopilot_OnAlreadyAtDestination()
        {
            OnAutopilotMessage.Invoke($"Autopilot is already at destination: {GetCurrentDestinationName()}.", false);
            if (IsTraveling()) AbortTask();
        }

        private void Autopilot_OnAbortAutopilot()
        {
            OnAutopilotMessage.Invoke($"Autopilot to destination '{GetCurrentDestinationName()}' has been aborted.", true);
            if (IsTraveling()) AbortTask();
        }

        private void OnEnterShip()
        {
            if (!playerHasEnteredShip)
            {
                playerHasEnteredShip = true;
                cockpitController._thrustController._shipAlignment.Start();
            }
        }
    }
}

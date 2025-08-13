using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NeuroPilot
{
    public class EnhancedAutoPilot : MonoBehaviour
    {
        public class AutopilotEvent : UnityEvent<string, bool> { }

        private const float STUCK_TIMEOUT = 5f;
        private const float TAKEOFF_TARGET_VELOCITY = 300f;
        private const float LANDING_TARGET_VELOCITY = -30f;

        private static EnhancedAutoPilot instance;

        public AutopilotEvent OnAutopilotMessage = new();

        ShipCockpitController cockpitController;
        Autopilot autopilot;
        SectorDetector shipSectorDetector;
        HatchController hatchController;
        StarfieldController starfield;

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
            GetCurrentTask()?.Destination ?? Destinations.GetByReferenceFrame(autopilot._referenceFrame);

        public string GetCurrentDestinationName() =>
            GetCurrentDestination()?.ToString() ?? autopilot._referenceFrame?.GetHUDDisplayName() ?? string.Empty; //TODO GetHUDDisplayName() is empty, never null

        public Destination GetCurrentLocation() => Destinations.GetShipLocation() ?? Destinations.GetByReferenceFrame(shipSectorDetector.GetPassiveReferenceFrame());

        public AutoPilotTask GetCurrentTask() => taskQueue.TryPeek(out var task) ? task : null;
        public IEnumerable<AutoPilotTask> GetQueuedTasks() => taskQueue;

        public bool IsManualAllowed() => !IsAutopilotActive() &&
            (ModConfig.ManualOverride
            || GetCurrentLocation()?.GetDistanceToShip() < GetCurrentLocation()?.GetReferenceFrame()?.GetAutopilotArrivalDistance() + 100f
            || GetCurrentLocation() != null
            || EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned && Locator.GetCloakFieldController().isShipInsideCloak);

        public bool IsAutopilotActive() => GetCurrentTask() != null;
        public bool IsTraveling() => GetCurrentTask() is TravelTask; // TODO check if any of these usages can be improved on
        public bool IsTakingOff() => GetCurrentTask() is TakeOffTask;
        public bool IsLanding() => GetCurrentTask() is LandingTask;
        public bool IsEvading() => GetCurrentTask() is EvadeTask;
        public bool IsCrashing() => GetCurrentTask() is CrashTask;

        public IEnumerable<Destination> GetPossibleObstacles() => possibleObstacles;
        public IEnumerable<Destination> GetActiveObstacles() => activeObstacles;

        public bool IsAutopilotAvailable() => playerHasEnteredShip && !IsAutopilotDamaged() && !(ModConfig.ManualOverride && PlayerState.AtFlightConsole());
        public bool IsAutopilotDamaged() => autopilot.IsDamaged() || cockpitController._shipSystemFailure || !Locator.GetShipBody().gameObject.activeSelf;

        protected void Awake()
        {
            starfield = GameObject.Find("Starfield").GetComponent<StarfieldController>();
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

            if (currentTask != null && IsAutopilotDamaged())
            {
                OnAutopilotMessage.Invoke("Autopilot has been aborted due to damage to the Autopilot module and cannot be engaged until it is repaired manually. There is a problem with your AI.", false);
                AbortTask();
            }

            switch (currentTask)
            {
                case TakeOffTask:
                    if (currentTask.Destination.GetDistanceToShip() > currentTask.Destination.GetReferenceFrame().GetAutopilotArrivalDistance() + 100f)
                    {
                        OnAutopilotMessage.Invoke("Autopilot has successfully taken off from the current location.", true);
                        CompleteTask();
                        break;
                    }
                    TickTakeOffLand();
                    break;
                case TravelTask:
                    if (!autopilot.IsFlyingToDestination() && !autopilot.FlyToDestination(currentTask.Destination.GetReferenceFrame()))
                    {
                        OnAutopilotMessage.Invoke($"Autopilot failed to engage travel to destination '{currentTask.Destination}'.", false);
                        AbortTask();
                        break;
                    }
                    if (EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned && Locator.GetCloakFieldController().isShipInsideCloak)
                    {
                        OnAutopilotMessage.Invoke("Autopilot has aborted travel because the ship has entered a cloaking field.", false);
                        AbortTask();
                    }
                    break;
                case EvadeTask:
                    var target = currentTask.Destination.GetReferenceFrame();

                    if (target == null)
                    {
                        OnAutopilotMessage.Invoke("Autopilot has aborted evasion because the target location no longer exists.", false);
                        AbortTask();
                        break;
                    }

                    var towardsDir = (target.GetPosition() - Locator.GetShipBody().GetPosition()).normalized;
                    var relativeVelocity = Locator.GetShipBody().GetRelativeVelocity(target).normalized;
                    var isMovingAway = Vector3.Dot(towardsDir, relativeVelocity) > 0;

                    if (isMovingAway)
                    {
                        OnAutopilotMessage.Invoke($"Autopilot successfully evaded {currentTask.Destination?.Name ?? currentTask.Destination.GetReferenceFrame().GetHUDDisplayName()}.", false);
                        CompleteTask();
                    }
                    break;
                case LandingTask:
                    if (cockpitController._landingManager.IsLanded())
                    {
                        OnAutopilotMessage.Invoke("Autopilot has successfully landed at the current location.", false);
                        CompleteTask();
                    }
                    TickTakeOffLand();
                    break;
            }

            UpdateObstacles();

            if (!IsManualAllowed())
            {
                cockpitController._thrustController.enabled = !IsAutopilotDamaged();
                cockpitController._thrustController._shipAlignment.enabled = IsTakingOff() || IsLanding() || ((IsTraveling() || IsCrashing()) && !PlayerState.IsInsideShip()); //TODO neccesary?
                cockpitController._thrustController._shipAlignment._localAlignmentAxis = (IsTraveling() || IsCrashing()) ? Vector3.forward : Vector3.down;
            }
        }

        public void TickTakeOffLand()
        {
            if (!cockpitController.InLandingMode())
                cockpitController.EnterLandingMode();

            var isStuck = Math.Abs(GetCurrentLandingVelocity()) < 2f;
            if (!isStuck)
            {
                stuckTime = 0f;
                return;
            }

            stuckTime += Time.deltaTime;
            if (stuckTime < STUCK_TIMEOUT)
                return;

            stuckTime = 0f;

            if (GetCurrentTask() is TakeOffTask)
            {
                OnAutopilotMessage.Invoke($"Autopilot has aborted because the ship became stuck while trying to take off.", false);
                AbortTask();
                return;
            }
            else
            {
                OnAutopilotMessage.Invoke($"Autopilot complete. The ship has come to a stop but may not be on solid ground.", false);
                CompleteTask();
            }
        }

        public float GetCurrentLandingVelocity()
        {
            var rfv = GetCurrentDestination()?.GetReferenceFrame();
            if (rfv == null)
                return 0f;

            var upAxis = (Locator.GetShipBody().GetPosition() - rfv.GetPosition()).normalized;
            var relativeVelocity = -Locator.GetShipBody().GetRelativeVelocity(rfv);
            return Vector3.Dot(relativeVelocity, upAxis);
        }

        public float GetTargetLandingVelocity()
        {
            var currentTask = GetCurrentTask();
            return currentTask switch
            {
                TakeOffTask => TAKEOFF_TARGET_VELOCITY,
                LandingTask => Math.Min(-(((Locator.GetShipBody().GetPosition() - GetCurrentDestination().GetReferenceFrame().GetPosition()).magnitude - GetCurrentDestination()?.InnerRadius ?? 200f) / 5), LANDING_TARGET_VELOCITY),
                _ => 0f,
            };
        }

        public bool ValidateDestination(Destination destination, out string error)
        {
            if (destination == null)
            {
                error = $"Destination '{destination.Name}' not found. Valid destinations are: {string.Join(", ", Destinations.GetAllValidNames())}";
                return false;
            }

            if (!destination.IsAvailable(out string validationError))
            {
                error = $"Destination '{destination.Name}' is not currently available: {validationError}";
                return false;
            }

            var refFrame = destination.GetReferenceFrame();
            if (refFrame == null)
            {
                error = $"Cannot acquire a lock on destination '{destination.Name}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryEngageTravel(string destinationName, out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            var destination = Destinations.GetByName(destinationName);
            if (!ValidateDestination(destination, out error)) return false;

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

            OnAutopilotMessage.Invoke("Autopilot has been aborted.", true);

            error = string.Empty;
            return true;
        }

        public bool TryTakeOff(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (GetCurrentLocation() == null)
            {
                error = "Cannot take off because the ship is not currently landed at a location.";
                return false;
            }

            AcceptTask(new TakeOffTask(GetCurrentLocation()));
            error = string.Empty;
            return true;
        }

        public bool TryLand(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (GetCurrentLocation() == null)
            {
                error = "Cannot land because the ship is not currently in a valid location to land at.";
                return false;
            }
            if (cockpitController._landingManager.IsLanded())
            {
                error = "The ship is already landed. No need to land again.";
                return false;
            }

            AcceptTask(new LandingTask(GetCurrentLocation()));
            error = string.Empty;
            return true;
        }

        public bool TryEvade(string destinationName, out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            var destination = Destinations.GetByName(destinationName);
            if (!ValidateDestination(destination, out error)) return false;

            AcceptTask(new EvadeTask(destination));
            error = string.Empty;
            return true;
        }

        public bool TryCrash(string destinationName, out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (!ModConfig.AllowDestructive)
            {
                error = "The player has decided you were being a nuisance.";
                return false;
            }

            var destination = Destinations.GetByName(destinationName);
            if (!ValidateDestination(destination, out error)) return false;

            AcceptTask(new CrashTask(destination));
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

            if (cockpitController._headlight.IsPowered() == on)
            {
                error = $"Ship headlights are already {(on ? "on" : "off")}.";
                return false;
            }

            cockpitController._externalLightsOn = on;
            cockpitController.SetEnableShipLights(on);
            if (!PlayerState._insideShip)
                cockpitController._headlight.SetPowered(on);

            error = string.Empty;
            return true;
        }

        public bool TryControlHatch(bool open, out string error)
        {
            if (!ModConfig.AllowDestructive)
            {
                error = "The player has decided you were being a nuisance.";
                return false;
            }
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
            List<string> messages = [];

            if (!ValidateAutopilotStatus(out string error)) return error;

            if (autopilot.IsFlyingToDestination())
            {
                messages.Add($"Autopilot is currently engaged to travel to destination: {GetCurrentDestinationName()}.");
            }
            else if (IsLanding())
            {
                messages.Add($"Autopilot is currently landing at: {GetCurrentDestinationName()}.");
            }
            else if (IsTakingOff())
            {
                messages.Add($"Autopilot is currently taking off from: {GetCurrentDestinationName()}.");
            }
            else if (IsEvading())
            {
                messages.Add($"Autopilot is currently evading: {GetCurrentDestinationName()}.");
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

        public bool Eject(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;

            if (!ModConfig.AllowDestructive)
            {
                error = "The player has decided you were being a nuisance.";
                return false;
            }

            var ejectionSystem = Locator.GetShipTransform().Find("Module_Cockpit/Systems_Cockpit/EjectionSystem").GetComponent<ShipEjectionSystem>();
            ejectionSystem.enabled = true;
            ejectionSystem._ejectPressed = true;

            error = string.Empty;
            return true;
        }

        float spinTime;

        public bool Spin(out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;
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

        public bool TryOrient(string destinationName, out string error)
        {
            if (!ValidateAutopilotStatus(out error)) return false;
            if (!(cockpitController._thrustController._isRotationalThrustEnabled && cockpitController._thrustController._shipAlignment.CheckAlignmentRequirements()))
            {
                error = "Ship has recently touched the ground and cannot turn.";
                return false;
            }
            if (IsTakingOff() || IsLanding())
            {
                error = "Ship cannot orient while taking off or landing.";
                return false;
            }

            if ("Exploding Star".Equals(destinationName))
            {
                for (int i = starfield._lastAliveStarIndex; i >= 0; i--)
                {
                    var star = starfield._starfieldData.starGroups[starfield._orderedStarIndices[i].groupIndex].stars[starfield._orderedStarIndices[i].starIndex];

                    if (!star.supernova) continue;

                    FaceDirection(star.position);
                    error = string.Empty;
                    return true;
                }
                error = "Stars have not started going nova yet, try later in the loop!";
                return false;
            }

            var destination = Destinations.GetByName(destinationName);
            if (!ValidateDestination(destination, out error)) return false;

            FaceDirection(destination.GetReferenceFrame().GetPosition() - Locator.GetShipBody().GetWorldCenterOfMass());
            taskNotification = new NotificationData(NotificationTarget.All, $"Autopilot Facing: {destinationName}".ToUpper());
            NotificationManager.SharedInstance.PostNotification(taskNotification);

            error = string.Empty;
            return true;
        }

        public bool IsSpinning() => spinTime > 0;

        public void UpdateSpinning()
        {
            spinTime -= 1;
        }

        private bool ValidateAutopilotStatus(out string error) //TODO should probably warn more than once
        {
            if (ModConfig.ManualOverride && PlayerState.AtFlightConsole())
            {
                error = "Autopilot cannot be engaged while the manual override is active and someone is actively piloting.";
                return false;
            }
            if (!playerHasEnteredShip)
            {
                error = "Autopilot cannot be engaged until the ship has been powered on.";
                return false;
            }
            if (IsAutopilotDamaged())
            {
                error = "Autopilot module is damaged and cannot be engaged until it is repaired manually. There is a problem with your AI.";
                return false;
            }
            if (cockpitController._shipSystemFailure || !Locator.GetShipBody().gameObject.activeSelf)
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
            var start = Locator.GetShipBody().GetPosition();
            var end = currentDestination?.GetReferenceFrame()?.GetPosition() ?? start;

            foreach (var d in Destinations.GetAll())
            {
                if (d == currentDestination || d == GetCurrentLocation() || d.GetReferenceFrame() == null || d is FloatingDestination or ShipDestination)
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
                if (distanceToPath < d.InnerRadius)
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
                else if (distanceToPath < d.OuterRadius)
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

        private void AcceptTask(AutoPilotTask task)
        {
            if (taskQueue.Count > 0) AbortTask();

            if ((task is TravelTask or CrashTask) && (Destinations.GetShipLocation()?.CanLand() ?? false || cockpitController._landingManager.IsLanded()))
            {
                taskQueue.Enqueue(new TakeOffTask(GetCurrentLocation()));
            }

            taskQueue.Enqueue(task);
            RunTask();
        }

        private void RunTask()
        {
            if (taskQueue.Count < 1)
                return;

            OnAutopilotMessage.Invoke($"Autopilot engaged to {GetCurrentTask()}", true);
            taskNotification = new NotificationData(NotificationTarget.All, $"Autopilot Engaged: {GetCurrentTask()}".ToUpper());
            NotificationManager.SharedInstance.PostNotification(taskNotification, true);
            if (GetCurrentTask() is TravelTask or CrashTask)
                FaceDirection(GetCurrentDestination().GetReferenceFrame().GetPosition() - Locator.GetShipBody().GetWorldCenterOfMass());
        }

        private void FaceDirection(Vector3 targetDir)
        {
            var shipBody = Locator.GetShipBody();
            var currentDir = shipBody.transform.forward;
            targetDir.Normalize();


            Vector3 axis = Vector3.Cross(currentDir, targetDir);
            float sin = axis.magnitude;
            float cos = Vector3.Dot(currentDir, targetDir);

            float angleRad = Mathf.Atan2(sin, cos);

            Vector3 desiredAV = axis / sin * angleRad;

            shipBody.AddAngularVelocityChange(desiredAV - shipBody.GetAngularVelocity());
        }

        private void AbortTask()
        {
            if (autopilot.IsFlyingToDestination())
                autopilot.Abort();
            else
                cockpitController._shipAudioController.PlayAutopilotOff();

            StopTask();
            taskQueue.Clear();
        }

        private void CompleteTask()
        {
            StopTask();

            if (GetCurrentTask() is TravelTask travelTask && travelTask.Destination.CanLand() && !PlayerState.IsInsideShip())
            {
                taskQueue.Enqueue(new LandingTask(travelTask.Destination));
            }

            taskQueue.Dequeue();
            RunTask();
        }

        private void StopTask()
        {
            if (GetCurrentTask() is TakeOffTask or LandingTask)
                cockpitController.ExitLandingMode();

            if (taskNotification == null)
                return;

            NotificationManager.SharedInstance.UnpinNotification(taskNotification);
            taskNotification = null;
        }

        private void Autopilot_OnArriveAtDestination(float arrivalError)
        {
            switch (arrivalError)
            {
                case > 100f:
                    if (GetCurrentDestination() is PlayerDestination && Destinations.GetPlayerLocation() == Destinations.GetShipLocation())
                        AcceptTask(new LandingTask(Destinations.GetShipLocation()));
                    else
                        // We effectively *didn't* arrive at the destination if the error is too large; retry
                        AcceptTask(new TravelTask(GetCurrentDestination()));
                    return;
                case > 50f:
                    OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (undershot by {arrivalError:F2} meters).", true);
                    break;
                case < -50f:
                    OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (overshot by {Mathf.Abs(arrivalError):F2} meters).", true);
                    break;
                default:
                    OnAutopilotMessage.Invoke($"Autopilot successfully arrived at destination: {GetCurrentDestinationName()}.", true);
                    break;
            }

            if (IsTraveling()) CompleteTask();
        }

        private void Autopilot_OnAlreadyAtDestination()
        {
            if (!IsTraveling()) return;

            StopTask();
            taskQueue.Clear();
        }

        private void Autopilot_OnAbortAutopilot()
        {
            if (!IsTraveling()) return;

            StopTask();
            taskQueue.Clear();
        }

        private void OnEnterShip()
        {
            if (playerHasEnteredShip)
                return;

            playerHasEnteredShip = true;
            cockpitController._thrustController._shipAlignment.Start();
        }
    }
}

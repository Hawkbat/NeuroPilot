using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace NeuroPilot
{
    public class EnhancedAutoPilot : MonoBehaviour
    {
        public class AutopilotEvent : UnityEvent<string> { }

        private const float STUCK_TIMEOUT = 5f;
        private const float TAKEOFF_TARGET_VELOCITY = 200f;
        private const float LANDING_TARGET_VELOCITY = -30f;

        private static EnhancedAutoPilot instance;

        public AutopilotEvent OnAutopilotMessage = new();

        ShipCockpitController cockpitController;
        Autopilot autopilot;
        SectorDetector shipSectorDetector;

        readonly Queue<AutoPilotTask> taskQueue = new();
        NotificationData taskNotification;
        bool playerHasEnteredShip;
        float stuckTime;

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

        public ReferenceFrame GetCurrentLocationReferenceFrame() => Destinations.GetShipLocation()?.GetReferenceFrame() ??
            shipSectorDetector.GetPassiveReferenceFrame();

        public AutoPilotTask GetCurrentTask() => taskQueue.TryPeek(out var task) ? task : null;
        public IEnumerable<AutoPilotTask> GetQueuedTasks() => taskQueue;
        public bool IsAutopilotActive() => GetCurrentTask() != null;
        public bool IsTraveling() => GetCurrentTask() is TravelTask;
        public bool IsTakingOff() => GetCurrentTask() is TakeOffTask;
        public bool IsLanding() => GetCurrentTask() is LandingTask;

        protected void Awake()
        {
            cockpitController = gameObject.GetComponent<ShipCockpitController>();
            autopilot = cockpitController._autopilot;
            shipSectorDetector = transform.root.GetComponentInChildren<SectorDetector>();

            autopilot.OnInitFlyToDestination += Autopilot_OnInitFlyToDestination;
            autopilot.OnInitMatchVelocity += Autopilot_OnInitMatchVelocity;
            autopilot.OnMatchedVelocity += Autopilot_OnMatchedVelocity;
            autopilot.OnFireRetroRockets += Autopilot_OnFireRetroRockets;
            autopilot.OnArriveAtDestination += Autopilot_OnArriveAtDestination;
            autopilot.OnAlreadyAtDestination += Autopilot_OnAlreadyAtDestination;
            autopilot.OnAbortAutopilot += Autopilot_OnAbortAutopilot;

            GlobalMessenger.AddListener("EnterShip", OnEnterShip);
        }

        protected void OnDestroy()
        {
            autopilot.OnInitFlyToDestination -= Autopilot_OnInitFlyToDestination;
            autopilot.OnInitMatchVelocity -= Autopilot_OnInitMatchVelocity;
            autopilot.OnMatchedVelocity -= Autopilot_OnMatchedVelocity;
            autopilot.OnFireRetroRockets -= Autopilot_OnFireRetroRockets;
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
                        if (currentTask is TakeOffTask)
                        {
                            OnAutopilotMessage.Invoke($"Autopilot has aborted because the ship became stuck while trying to take off.");
                            AbortTask();

                        }
                        else
                        {
                            OnAutopilotMessage.Invoke($"Autopilot complete. The ship has come to a stop but may not be on solid ground.");
                            CompleteTask();
                        }
                    }
                }
                else
                {
                    stuckTime = 0f;
                }
            }
            else
            {
                if (cockpitController.InLandingMode())
                {
                    cockpitController.ExitLandingMode();
                }
            }
            if (currentTask is TakeOffTask takeOffTask)
            {
                if (GetCurrentLocationReferenceFrame() != takeOffTask.location)
                {
                    OnAutopilotMessage.Invoke("Autopilot has successfully taken off from the current location.");
                    CompleteTask();
                }
            }
            if (currentTask is LandingTask landTask)
            {
                if (GetCurrentLocationReferenceFrame() != landTask.location)
                {
                    OnAutopilotMessage.Invoke("Autopilot has aborted landing because the ship is no longer at the intended landing location.");
                    AbortTask();
                }
                else if (cockpitController._landingManager.IsLanded())
                {
                    OnAutopilotMessage.Invoke("Autopilot has successfully landed at the current location.");
                    CompleteTask();
                }
            }
            if (currentTask is TravelTask travelTask)
            {
                if (!autopilot.IsFlyingToDestination() && !autopilot.FlyToDestination(travelTask.destination.GetReferenceFrame()))
                {
                    OnAutopilotMessage.Invoke($"Autopilot failed to engage travel to destination '{travelTask.destination}'.");
                    AbortTask();
                }
                if (Locator.GetCloakFieldController().isShipInsideCloak)
                {
                    OnAutopilotMessage.Invoke("Autopilot has aborted travel because the ship has entered a cloaking field.");
                    CompleteTask();
                }
            }
            cockpitController._thrustController.enabled = !cockpitController._shipSystemFailure;
            cockpitController._thrustController._shipAlignment.enabled = IsAutopilotActive();
            cockpitController._thrustController._shipAlignment._localAlignmentAxis = IsTraveling() ? Vector3.forward : Vector3.down;
        }

        public float GetCurrentLandingVelocity()
        {
            var rfv = GetCurrentLocationReferenceFrame();
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
            if (currentTask is LandingTask) return LANDING_TARGET_VELOCITY;
            return 0f;
        }

        public bool TryEngageTravel(string destinationName, out string error)
        {
            if (!playerHasEnteredShip)
            {
                error = "Autopilot cannot be engaged until the ship has been powered on.";
                return false;
            }

            if (autopilot.IsDamaged())
            {
                error = "Autopilot module is damaged and cannot be engaged until it is repaired manually.";
                return false;
            }

            if (autopilot.IsFlyingToDestination())
            {
                error = $"Autopilot is already engaged to travel to '{GetCurrentDestinationName()}'. Please abort the current travel before setting a new destination.";
                return false;
            }

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

            autopilot.Abort();
            AbortTask();
            OnAutopilotMessage.Invoke("Autopilot travel has been aborted.");

            error = string.Empty;
            return true;
        }

        public bool TryTakeOff(out string error)
        {
            if (!playerHasEnteredShip)
            {
                error = "Autopilot cannot be engaged until the ship has been powered on.";
                return false;
            }
            if (autopilot.IsDamaged())
            {
                error = "Autopilot module is damaged and cannot be engaged until it is repaired manually.";
                return false;
            }
            if (autopilot.IsFlyingToDestination())
            {
                error = $"Autopilot is already engaged to travel to '{GetCurrentDestinationName()}'. Please abort the current travel before taking off.";
                return false;
            }
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
            if (!playerHasEnteredShip)
            {
                error = "Autopilot cannot be engaged until the ship has been powered on.";
                return false;
            }
            if (autopilot.IsDamaged())
            {
                error = "Autopilot module is damaged and cannot be engaged until it is repaired manually.";
                return false;
            }
            if (autopilot.IsFlyingToDestination())
            {
                error = $"Autopilot is already engaged to travel to '{GetCurrentDestinationName()}'. Please abort the current travel before landing.";
                return false;
            }
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

        public string GetAutopilotStatus()
        {
            if (!playerHasEnteredShip)
            {
                return "Autopilot is not available. The ship has not been powered on yet.";
            }

            if (autopilot.IsDamaged())
            {
                return "Autopilot module is damaged and cannot be engaged until it is repaired manually.";
            }

            if (autopilot.IsFlyingToDestination())
            {
                return $"Autopilot is currently engaged to travel to destination: {GetCurrentDestinationName()}.";
            }

            return $"Autopilot is currently idle. You can engage it to travel to a destination.";
        }

        private void AcceptTask(AutoPilotTask task)
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
            if (taskQueue.Count > 0)
            {
                taskNotification = new NotificationData(NotificationTarget.All, $"Autopilot Engaged: {GetCurrentTask()}".ToUpper());
                NotificationManager.SharedInstance.PostNotification(taskNotification, true);
            }
        }

        private void AbortTask()
        {
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
            taskQueue.Dequeue();
            if (taskQueue.Count > 0)
            {
                taskNotification = new NotificationData(NotificationTarget.All, $"Autopilot Engaged: {GetCurrentTask()}".ToUpper());
                NotificationManager.SharedInstance.PostNotification(taskNotification, true);
            }
        }

        private void Autopilot_OnInitFlyToDestination()
        {
            OnAutopilotMessage.Invoke($"Autopilot engaged to travel to destination: {GetCurrentDestinationName()}.");
        }

        private void Autopilot_OnInitMatchVelocity()
        {
            OnAutopilotMessage.Invoke($"Autopilot is matching velocity with destination: {GetCurrentDestinationName()}.");
        }

        private void Autopilot_OnMatchedVelocity()
        {
            OnAutopilotMessage.Invoke($"Autopilot has matched velocity with destination: {GetCurrentDestinationName()} and will begin accelerating towards it.");
        }

        private void Autopilot_OnFireRetroRockets()
        {
            OnAutopilotMessage.Invoke($"Autopilot is firing retro rockets to decelerate before arriving at destination: {GetCurrentDestinationName()}.");
        }

        private void Autopilot_OnArriveAtDestination(float arrivalError)
        {
            if (Math.Abs(arrivalError) > 500f)
            {
                // We effectively *didn't* arrive at the destination if the error is too large; retry
                AcceptTask(new TravelTask(GetCurrentDestination()));
                return;
            }
            if (arrivalError > 50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (undershot by {arrivalError:F2} meters).");
            }
            else if (arrivalError < -50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (overshot by {Mathf.Abs(arrivalError):F2} meters).");
            }
            else
            {
                OnAutopilotMessage.Invoke($"Autopilot successfully arrived at destination: {GetCurrentDestinationName()}.");
            }

            CompleteTask();
        }

        private void Autopilot_OnAlreadyAtDestination()
        {
            OnAutopilotMessage.Invoke($"Autopilot is already at destination: {GetCurrentDestinationName()}.");
            AbortTask();
        }

        private void Autopilot_OnAbortAutopilot()
        {
            OnAutopilotMessage.Invoke($"Autopilot to destination '{GetCurrentDestinationName()}' has been aborted.");
            AbortTask();
        }

        private void OnEnterShip()
        {
            if (!playerHasEnteredShip)
            {
                playerHasEnteredShip = true;
                OnAutopilotMessage.Invoke("The ship has been powered on. Autopilot can now be engaged at any time.");
            }
        }
    }
}

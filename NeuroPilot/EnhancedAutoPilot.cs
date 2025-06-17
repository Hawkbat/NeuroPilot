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

        private static EnhancedAutoPilot instance;

        public AutopilotEvent OnAutopilotMessage = new();

        ShipCockpitController cockpitController;
        Autopilot autopilot;
        SectorDetector shipSectorDetector;

        Destination currentDestination;
        bool playerHasEnteredShip;

        public static EnhancedAutoPilot GetInstance()
        {
            if (instance) return instance;
            var shipCockpitController = FindObjectOfType<ShipCockpitController>();
            instance = shipCockpitController.gameObject.GetAddComponent<EnhancedAutoPilot>();
            return instance;
        }

        public Destination GetCurrentDestination() => currentDestination ?? Destinations.GetByReferenceFrame(autopilot._referenceFrame);

        public string GetCurrentDestinationName() => currentDestination?.name ?? autopilot._referenceFrame?.GetHUDDisplayName() ?? string.Empty;

        public Destination GetCurrentLocation() => Destinations.GetByReferenceFrame(shipSectorDetector.GetPassiveReferenceFrame());

        public string GetCurrentLocationName()
        {
            var rf = shipSectorDetector.GetPassiveReferenceFrame();
            if (rf != null)
            {
                return Destinations.GetByReferenceFrame(rf)?.name ?? rf.GetHUDDisplayName() ?? "Unknown Location";
            }
            return "Unknown Location";
        }

        public ReferenceFrame GetLandingTargetReferenceFrame()
        {
            return shipSectorDetector.GetPassiveReferenceFrame() ?? Locator._rfTracker.GetReferenceFrame();
        }

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
            if (!cockpitController.InLandingMode() && cockpitController.IsLandingModeAvailable())
            {
                cockpitController.EnterLandingMode();
            }
            if (cockpitController.InLandingMode() && !cockpitController.IsLandingModeAvailable())
            {
                cockpitController.ExitLandingMode();
            }
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
                error = $"Destination '{destinationName}' not found. Valid destinations are: {string.Join(", ", Destinations.GetAllNames())}";
                return false;
            }

            var refFrame = destination.GetReferenceFrame();
            if (refFrame == null)
            {
                error = $"Cannot acquire a lock on destination '{destinationName}'.";
                return false;
            }

            currentDestination = destination;

            if (!autopilot.FlyToDestination(refFrame))
            {
                currentDestination = null;
                error = $"Failed to engage autopilot to '{destinationName}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryAbortTravel(out string error)
        {
            if (!autopilot.IsFlyingToDestination())
            {
                error = "Autopilot is not currently engaged.";
                return false;
            }

            autopilot.Abort();
            OnAutopilotMessage.Invoke("Autopilot travel has been aborted.");

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
            if (arrivalError > 50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (undershot by {arrivalError:F2} meters).");
            }
            else if (arrivalError < 50f)
            {
                OnAutopilotMessage.Invoke($"Autopilot arrived at destination: {GetCurrentDestinationName()} (overshot by {Mathf.Abs(arrivalError):F2} meters).");
            }
            else
            {
                OnAutopilotMessage.Invoke($"Autopilot successfully arrived at destination: {GetCurrentDestinationName()}.");
            }

            currentDestination = null;
        }

        private void Autopilot_OnAlreadyAtDestination()
        {
            OnAutopilotMessage.Invoke($"Autopilot is already at destination: {GetCurrentDestinationName()}.");
            currentDestination = null;
        }

        private void Autopilot_OnAbortAutopilot()
        {
            OnAutopilotMessage.Invoke($"Autopilot to destination '{GetCurrentDestinationName()}' has been aborted.");
            currentDestination = null;
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

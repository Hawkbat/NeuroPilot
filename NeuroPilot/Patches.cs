using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroPilot
{
    [HarmonyPatch]
    public static class Patches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(ReferenceFrame), nameof(ReferenceFrame.GetAllowAutopilot))]
        public static bool ReferenceFrame_GetAllowAutopilot(ref bool __result)
        {
            // Allow autopilot to any reference frame
            __result = true;
            return false;
        }

        public static bool ReferenceFrame_GetAutopilotArrivalDistance(ReferenceFrame __instance, ref float __result)
        {
            // Override the distance at which autopilot considers the ship to have arrived at the destination
            var destination = Destinations.GetByReferenceFrame(__instance);
            if (destination != null)
            {
                __result = destination.GetInnerRadius();
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Locator), nameof(Locator.GetReferenceFrame))]
        public static bool Locator_GetReferenceFrame(ref ReferenceFrame __result)
        {
            // Override reference frame used as the target for landing mode, match velocity, etc.
            // Surely this will have no negative ramifications
            __result = EnhancedAutoPilot.GetInstance().GetCurrentLocationReferenceFrame() ?? Locator._rfTracker.GetReferenceFrame();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.OnTargetReferenceFrame))]
        public static bool ShipCockpitController_OnTargetReferenceFrame()
        {
            // Prevent automatic canceling of autopilot
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.OnUntargetReferenceFrame))]
        public static bool ShipCockpitController_OnUntargetReferenceFrame()
        {
            // Prevent automatic canceling of autopilot
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.Update))]
        public static bool ShipCockpitController_Update(ShipCockpitController __instance)
        {
            // Copy-paste of the Update method to strip out user input for autopilot and landing modes
            // Maybe could be done with patching OWInput.IsNewlyPressed() while in InputMode.ShipCockpit but that's even scarier

            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
            {
                // If manual override is enabled and player is piloting, allow the original Update method to run and give full control
                return true;
            }

            if (!__instance._playerAtFlightConsole)
            {
                return false;
            }
            if (__instance._controlsLocked && Time.time >= __instance._controlsUnlockTime)
            {
                __instance._controlsLocked = false;
                __instance._thrustController.enabled = !__instance._shipSystemFailure;
                if (!__instance._shipSystemFailure)
                {
                    if (__instance._thrustController.RequiresIgnition() && __instance._landingManager.IsLanded())
                    {
                        RumbleManager.SetShipThrottleCold();
                    }
                    else
                    {
                        RumbleManager.SetShipThrottleNormal();
                    }
                }
            }
            if (!__instance._autopilot.IsFlyingToDestination())
            {
                if (__instance.IsMatchVelocityAvailable(false) && OWInput.IsNewlyPressed(InputLibrary.matchVelocity, InputMode.All))
                {
                    __instance._autopilot.StartMatchVelocity(Locator.GetReferenceFrame(false), false);
                }
                else if (__instance._autopilot.IsMatchingVelocity() && !__instance._autopilot.IsFlyingToDestination() && OWInput.IsNewlyReleased(InputLibrary.matchVelocity, InputMode.All))
                {
                    __instance._autopilot.StopMatchVelocity();
                }
            }
            if (__instance.UsingLandingCam())
            {
                if (__instance._enteringLandingCam)
                {
                    __instance.UpdateEnterLandingCamTransition();
                }
                if (!__instance._isLandingMode && __instance.IsLandingModeAvailable())
                {
                    __instance.EnterLandingMode();
                }
                else if (__instance._isLandingMode && !__instance.IsLandingModeAvailable())
                {
                    __instance.ExitLandingMode();
                }
                __instance._playerAttachOffset = Vector3.MoveTowards(__instance._playerAttachOffset, Vector3.zero, Time.deltaTime);
            }
            else
            {
                __instance._playerAttachOffset = __instance._thrusterModel.GetLocalAcceleration() / __instance._thrusterModel.GetMaxTranslationalThrust() * -0.2f;
                if (OWInput.IsInputMode(InputMode.ShipCockpit | InputMode.LandingCam) && Locator.GetToolModeSwapper().GetToolMode() == ToolMode.None && OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                {
                    __instance.ExitFlightConsole();
                }
            }
            __instance._playerAttachPoint.SetAttachOffset(__instance._playerAttachOffset);

            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ShipPromptController), nameof(ShipPromptController.Update))]
        public static void ShipPromptController_Update(ShipPromptController __instance)
        {
            // Prevent autopilot prompts from showing up

            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
            {
                // If manual override is enabled and player is piloting, keep prompts visible
                return;
            }

            __instance._autopilotPrompt.SetVisibility(false);
            __instance._landingModePrompt.SetVisibility(false);
            __instance._exitLandingCamPrompt.SetVisibility(false);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AlignShipWithReferenceFrame), nameof(AlignShipWithReferenceFrame.GetAlignmentDirection))]
        public static bool AlignShipWithReferenceFrame_GetAlignmentDirection(AlignShipWithReferenceFrame __instance, ref Vector3 __result)
        {
            // Override alignment while autopilot is active
            var autopilot = EnhancedAutoPilot.GetInstance();
            if (!autopilot.IsAutopilotActive())
            {
                // If autopilot is not active, use the original method
                return true;
            }

            __result = __instance._currentDirection;

            if (autopilot.IsTakingOff() || autopilot.IsLanding())
            {
                var task = EnhancedAutoPilot.GetInstance().GetCurrentTask();
                ReferenceFrame rf = null;

                if (task is LandingTask landingTask)
                {
                    rf = landingTask.location;
                }
                else if (task is TakeOffTask takeOffTask)
                {
                    rf = takeOffTask.location;
                }
                if (rf != null)
                {
                    __result = rf.GetPosition() - __instance._owRigidbody.GetWorldCenterOfMass();
                }
            }
            else if (autopilot.IsTraveling())
            {
                var rf = autopilot.GetCurrentDestination()?.GetReferenceFrame();
                if (rf != null)
                {
                    __result = rf.GetPosition() - __instance._owRigidbody.GetWorldCenterOfMass();
                }
            }
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipThrusterController), nameof(ShipThrusterController.ReadTranslationalInput))]
        public static bool ShipThrusterController_ReadTranslationalInput(ShipThrusterController __instance, ref Vector3 __result)
        {
            // If manual override is enabled and player is piloting, allow player input
            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
            {
                return true;
            }

            var autopilot = EnhancedAutoPilot.GetInstance();

            // Override translational input for autopilot control
            if (autopilot.IsAutopilotActive())
            {
                if (__instance._requireIgnition)
                {
                    __instance._requireIgnition = false;
                    GlobalMessenger.FireEvent("StartShipIgnition");
                    GlobalMessenger.FireEvent("CompleteShipIgnition");
                    RumbleManager.PlayShipIgnition();
                    RumbleManager.SetShipThrottleNormal();
                }

                // If the ship is in autopilot mode, return the autopilot's thrust vector
                if (autopilot.IsTakingOff() || autopilot.IsLanding())
                {
                    var currentVelocity = autopilot.GetCurrentLandingVelocity();
                    var targetVelocity = autopilot.GetTargetLandingVelocity();
                    var smoothingRange = 20f;

                    var unclampedThrust = (targetVelocity - currentVelocity) / smoothingRange;
                    var thrust = Mathf.Clamp(unclampedThrust, -1f, 1f);
                    __result = Vector3.up * thrust;
                    return false;
                }

                if (autopilot.IsEvading())
                {
                    var target = ((EvadeTask)autopilot.GetCurrentTask()).location;
                    if (target != null)
                    {
                        var dir = __instance._shipBody.GetWorldCenterOfMass() - target.GetPosition();
                        __result = __instance.transform.InverseTransformDirection(dir.normalized);
                        return false;
                    }
                }
            }

            var loc = autopilot.GetCurrentLocation();
            if (loc != null)
            {
                // Allow the player to control the ship while within the current location's inner radius plus a fudge factor in case autopilot undershot
                if (loc.GetDistanceToShip() < loc.GetReferenceFrame().GetAutopilotArrivalDistance() + 100f)
                {
                    return true;
                }
                else
                {
                    __result = Vector3.zero;
                    return false;
                }
            }

            // Fallback if there's no destination and there's still a reference frame nearby
            var rf = autopilot.GetCurrentLocationReferenceFrame();
            if (rf != null) return true;

            __result = Vector3.zero;
            return false;
        }
    }
}

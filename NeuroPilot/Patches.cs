using HarmonyLib;
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

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.FixedUpdate))]
        public static bool Autopilot_FixedUpdate(ShipCockpitController __instance)
        {
            // Prevent automatic autopilot to sun
            if (!__instance._playerAtFlightConsole)
            {
                __instance._playerAttachPoint.transform.localPosition = Vector3.Lerp(__instance._origAttachPointLocalPos, __instance._raisedAttachPointLocalPos, Mathf.InverseLerp(__instance._exitFlightConsoleTime, __instance._exitFlightConsoleTime + 0.2f, Time.time));
                if ((double)Time.time < (double)__instance._exitFlightConsoleTime + 0.20000000298023224)
                    return false;
                __instance.CompleteExitFlightConsole();
            }
            else {
                __instance._playerAttachPoint.transform.localPosition = Vector3.Lerp(__instance._raisedAttachPointLocalPos, __instance._origAttachPointLocalPos, Mathf.InverseLerp(__instance._enterFlightConsoleTime, __instance._enterFlightConsoleTime + 0.4f, Time.time));
            }
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
            var autopilot = EnhancedAutoPilot.GetInstance();

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
                if (autopilot.IsManualAllowed())
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
            }
            if (OWInput.IsInputMode(InputMode.ShipCockpit | InputMode.LandingCam)) {
                if (!__instance._enteringLandingCam)
                {
                    if (!__instance.UsingLandingCam() && OWInput.IsNewlyPressed(InputLibrary.landingCamera) && !OWInput.IsPressed(InputLibrary.freeLook))
                        __instance.EnterLandingView();
                    else if (__instance.UsingLandingCam() && (OWInput.IsNewlyPressed(InputLibrary.landingCamera) || OWInput.IsNewlyPressed(InputLibrary.cancel)))
                    {
                        InputLibrary.cancel.ConsumeInput();
                        __instance.ExitLandingView();
                    }
                }
                if (__instance.UsingLandingCam())
                {
                    if (__instance._enteringLandingCam)
                    {
                        __instance.UpdateEnterLandingCamTransition();
                    }
                    if (!__instance._isLandingMode && __instance.IsLandingModeAvailable() && autopilot.IsManualAllowed())
                    {
                        __instance.EnterLandingMode();
                    }
                    else if (__instance._isLandingMode && (!__instance.IsLandingModeAvailable() || !autopilot.IsManualAllowed()))
                    {
                        __instance.ExitLandingMode();
                    }
                    __instance._playerAttachOffset = Vector3.MoveTowards(__instance._playerAttachOffset, Vector3.zero, Time.deltaTime);
                }
                else
                {
                    __instance._playerAttachOffset = __instance._thrusterModel.GetLocalAcceleration() / __instance._thrusterModel.GetMaxTranslationalThrust() * -0.2f;
                    if (Locator.GetToolModeSwapper().GetToolMode() == ToolMode.None && OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                    {
                        __instance.ExitFlightConsole();
                    }
                }
            }
            __instance._playerAttachPoint.SetAttachOffset(__instance._playerAttachOffset);

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.IsLandingModeAvailable))]
        public static bool ShipCockpitController_IsLandingModeAvailable(ShipCockpitController __instance, ref bool __result)
        {
            if (!EnhancedAutoPilot.GetInstance().IsManualAllowed())
            {
                __result = false;
                return false;
            }
            return true;
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
            __instance._abortAutopilotPrompt.SetVisibility(false);
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

                    var task = autopilot.GetCurrentTask();
                    ReferenceFrame rfv = null;
                    if (task is LandingTask landingTask)
                        rfv= landingTask.location;
                    else if (task is TakeOffTask takeOffTask)
                        rfv= takeOffTask.location;


                    Vector3 unclampedThrust = Vector3.zero;
                    if (rfv != null) {
                        unclampedThrust = __instance._shipBody.transform.InverseTransformDirection((rfv.GetVelocity() - __instance._shipBody.GetVelocity()) * .5f);
                    }

                    var downThrust = (targetVelocity - currentVelocity) / smoothingRange;
                    unclampedThrust.y = downThrust;
                    var thrust = Vector3.ClampMagnitude(unclampedThrust, 1f);
                    __result = thrust;
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

            // Allow the player to control the ship while within the current location's inner radius plus a fudge factor in case autopilot undershot
            if (autopilot.IsManualAllowed())
                return true;
            else
            {
                __result = Vector3.zero;
                return false;
            }
        }
    }
}

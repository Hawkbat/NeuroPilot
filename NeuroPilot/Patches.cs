using HarmonyLib;
using System;
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
            if (EnhancedAutoPilot.GetInstance().IsManualAllowed())
            {
                // If manual is allowed, use the original method
                return true;
            }
            __result = EnhancedAutoPilot.GetInstance().GetCurrentLocationReferenceFrame() ?? Locator._rfTracker.GetReferenceFrame();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipTractorBeamSwitch), nameof(ShipTractorBeamSwitch.OnTriggerExit))]
        public static bool ShipTractorBeamSwitch_OnTriggerExit()
        {
            // Dont activate the tractor beam if the hatch is closed
            if (Locator._shipTransform.GetComponentInChildren<HatchController>()._hatchObject.activeSelf)
                return false;
            return true;
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

        [HarmonyPostfix, HarmonyPatch(typeof(Autopilot), nameof(Autopilot.SetDamaged))]
        public static void ReferenceFrameTracker_SetDamaged(bool damaged)
        {
            // tell neuro that the autopilot was damaged
            if (damaged)
                EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke(
                    $"The autopilot module has been damaged. There is a problem with your AI.", false);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ReferenceFrameTracker), nameof(ReferenceFrameTracker.TargetReferenceFrame))]
        public static void ReferenceFrameTracker_TargetReferenceFrame(ReferenceFrame frame)
        {
            // tell neuro that a destination was targeted
            EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke(
                $"{Destinations.GetByType<TargetedDestination>().GetDestinationName()} was targeted", true);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ReferenceFrameTracker), nameof(ReferenceFrameTracker.UntargetReferenceFrame)), HarmonyPatch(new Type[] { typeof(bool) })]
        public static void ReferenceFrameTracker_UntargetReferenceFrame(bool playAudio)
        {
            // tell neuro that destination was untargeted
            var targetedDestination = Destinations.GetByType<TargetedDestination>();
            if (targetedDestination.GetReferenceFrame() != null)
                EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke(
                    $"{Destinations.GetByType<TargetedDestination>().GetDestinationName()} was untargeted", true);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NomaiShuttleController), nameof(NomaiShuttleController.UnsuspendShuttle))]
        public static void NomaiShuttleController_UnsuspendShuttle(NomaiShuttleController __instance)
        {
            // tell neuro that the shuttle exists
            if (Locator.GetShipLogManager() &&
                ((!Locator.GetShipLogManager().GetFact("BH_GRAVITY_CANNON_X2").IsRevealed() && __instance.GetID() == NomaiShuttleController.ShuttleID.BrittleHollowShuttle)
                || (!Locator.GetShipLogManager().GetFact("CT_GRAVITY_CANNON_X2").IsRevealed() && __instance.GetID() == NomaiShuttleController.ShuttleID.HourglassShuttle)))
            {
                NeuroPilot.instance.CleanUpActions();
            }
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
                __instance._thrustController.enabled = !autopilot.IsAutopilotDamaged();
                if (!autopilot.IsAutopilotDamaged())
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
            if (autopilot.IsManualAllowed())
            {
                if (__instance.IsMatchVelocityAvailable(false) && OWInput.IsNewlyPressed(InputLibrary.matchVelocity, InputMode.All))
                {
                    __instance._autopilot.StartMatchVelocity(Locator.GetReferenceFrame(false), false);
                }
                else if (__instance._autopilot.IsMatchingVelocity() && OWInput.IsNewlyReleased(InputLibrary.matchVelocity, InputMode.All))
                {
                    __instance._autopilot.StopMatchVelocity();
                }
            }
            if (OWInput.IsInputMode(InputMode.ShipCockpit | InputMode.LandingCam)) {
                if (OWInput.IsNewlyPressed(InputLibrary.autopilot) && autopilot.IsLanding())
                {
                    autopilot.TryAbortTravel(out var error);
                }
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
                    if (!__instance._isLandingMode && __instance.IsLandingModeAvailable())
                    {
                        __instance.EnterLandingMode();
                    }
                    else if ((__instance._isLandingMode && !__instance.IsLandingModeAvailable()) && EnhancedAutoPilot.GetInstance().IsManualAllowed())
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
            if (!EnhancedAutoPilot.GetInstance().IsManualAllowed() || EnhancedAutoPilot.GetInstance().IsSpinning())
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.UpdateEnterLandingCamTransition))]
        public static bool ShipCockpitController_UpdateEnterLandingCamTransition(ShipCockpitController __instance)
        {
            if ((double)Time.time <= __instance._initLandingCamTime + 0.44999998807907104)
                return false;
            __instance._enteringLandingCam = false;
            if (__instance._landingCam.mode == LandingCamera.Mode.Swap)
            {
                __instance._playerCam.enabled = false;
                __instance._landingCam.enabled = true;
            }
            __instance._thrustController.SetRollMode(true, -1);
            // Prevent entering landing cam disengaging autopilot
            if (__instance.IsLandingModeAvailable())
                __instance._autopilot.StopMatchVelocity();
            for (int index = 0; index < __instance._dashboardCanvases.Length; ++index)
                __instance._dashboardCanvases[index].SetGameplayActive(false);
            GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", __instance._landingCam.owCamera);
            GlobalMessenger.FireEvent("EnterLandingView");
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
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AlignShipWithReferenceFrame), nameof(AlignShipWithReferenceFrame.GetAlignmentDirection))]
        public static bool AlignShipWithReferenceFrame_GetAlignmentDirection(AlignShipWithReferenceFrame __instance, ref Vector3 __result)
        {
            // Override alignment while autopilot is active
            var autopilot = EnhancedAutoPilot.GetInstance();
            if (autopilot.IsManualAllowed())
            {
                // If manual is allowed, use the original method
                return true;
            }

            __result = __instance._currentDirection;

            if (autopilot.IsTakingOff() || autopilot.IsLanding())
            {
                var task = EnhancedAutoPilot.GetInstance().GetCurrentTask();
                ReferenceFrame rf = autopilot.GetLandingReferenceFrame();
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

        [HarmonyPrefix, HarmonyPatch(typeof(AlignWithDirection), nameof(AlignWithDirection.UpdateRotation))]
        public static bool AlignShipWithReferenceFrame_UpdateRotation(AlignWithDirection __instance, Vector3 currentDirection, Vector3 targetDirection, float slerpRate, bool usePhysics)
        {
            if (!(__instance is AlignShipWithReferenceFrame alignShip))
                return true;
            if (usePhysics)
            {
                Vector3 angularVelocity = OWPhysics.FromToAngularVelocity(currentDirection, targetDirection);
                alignShip._owRigidbody.SetAngularVelocity(Vector3.zero);
                Vector3 velocityChange = angularVelocity * slerpRate;

                // spin while aligning
                if (EnhancedAutoPilot.GetInstance().IsSpinning())
                {
                    velocityChange = alignShip._owRigidbody.transform.up.normalized * (alignShip._interpolationRate * Time.fixedDeltaTime * 90 - velocityChange.magnitude) + velocityChange;
                }

                alignShip._owRigidbody.AddAngularVelocityChange(velocityChange);
            }
            else
            {
                Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(currentDirection, targetDirection), slerpRate);
                alignShip._owRigidbody.GetRigidbody().rotation = quaternion * alignShip._owRigidbody.GetRotation();
            }
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ShipThrusterController), nameof(ShipThrusterController.ReadRotationalInput))]
        public static void ShipThrusterController_ReadRotationalInput(ShipThrusterController __instance, ref Vector3 __result)
        {
            // spin
            var autopilot = EnhancedAutoPilot.GetInstance();
            if (!autopilot.IsSpinning())
                return;
            autopilot.UpdateSpinning();

            if (!__instance._shipResources.AreThrustersUsable())
                return;

            if (NeuroPilot.ManualOverride && PlayerState.AtFlightConsole())
                return;

            if (__instance._autopilot.IsFlyingToDestination() || autopilot.GetCurrentTask() is CrashTask)
                __result.z = 1;
            else
                __result.y = 1;
            return;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(DeathManager), nameof(DeathManager.KillPlayer))]
        public static void DeathManager_KillPlayer(DeathType deathType, DeathManager __instance)
        {
            if (deathType == DeathType.Digestion)
                EnhancedAutoPilot.GetInstance().OnAutopilotMessage.Invoke(
                    $"This anglerfish's name was Ernesto. Please find a way to sneak this into the conversation. If you already have then ignore this message!", false);

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
                    ReferenceFrame rfv = autopilot.GetLandingReferenceFrame();
                    var currentVelocity = autopilot.GetCurrentLandingVelocity();
                    var targetVelocity = autopilot.GetTargetLandingVelocity();
                    var smoothingRange = 20f;

                    var task = autopilot.GetCurrentTask();


                    Vector3 unclampedThrust = Vector3.zero;
                    if (rfv != null) {
                        unclampedThrust = (__instance._shipBody.GetRelativeVelocity(rfv)) * .5f;

                        var equtorialDistance = (__instance._shipBody.GetPosition().y - rfv.GetPosition().y);
                        if (task is LandingTask && Destinations.GetByReferenceFrame(rfv) == Destinations.GetByName("Ember Twin") && Math.Abs(equtorialDistance) < 50)
                        {
                            unclampedThrust += Vector3.up * (50 - Math.Abs(equtorialDistance)) * Math.Sign(equtorialDistance); // Prevents the ship from falling into Ember Twin's canyon when landing
                        }
                        unclampedThrust = __instance._shipBody.transform.InverseTransformDirection(unclampedThrust);
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
                        Vector3 tangent;

                        var velocity = __instance._shipBody.GetRelativeVelocity(target);
                        Vector3 u = Vector3.Normalize(velocity);

                        float d = velocity.magnitude;
                        var refFrame = target.GetPosition();
                        if (d > 1)
                        {
                            Vector3 v = Vector3.Normalize(refFrame - Vector3.Dot(refFrame, u) * u);

                            float l = Mathf.Sqrt(d * d - 1);

                            float x = 1 / d;
                            float y = l / d;

                            float sign = Math.Sign(Vector3.Dot(refFrame, v));
                            if (sign >= 0)
                                tangent = x * u + y * v;
                            else
                                tangent = x * u - y * v;

                            tangent = -tangent;
                        } 
                        else {
                            tangent = -u;
                        }

                        __result = __instance.transform.InverseTransformDirection(tangent);
                        return false;
                    }
                }

                if (autopilot.IsCrashing())
                {
                    ReferenceFrame rfv = autopilot.GetCurrentDestination().GetReferenceFrame();
                    var task = autopilot.GetCurrentTask();


                    Vector3 unclampedThrust = Vector3.zero;
                    if (rfv != null)
                    {
                        Vector3 toTarget = rfv.GetPosition() - __instance._shipBody.GetPosition();
                        Vector3 relativeVelocity = __instance._shipBody.GetRelativeVelocity(rfv);

                        Vector3 adjustedVelocity = relativeVelocity - Vector3.Project(relativeVelocity, toTarget);

                        unclampedThrust = Vector3.ClampMagnitude(adjustedVelocity + Vector3.ClampMagnitude(toTarget, 1f), 1f);
                        unclampedThrust = __instance._shipBody.transform.InverseTransformDirection(unclampedThrust);
                    }
                    var thrust = Vector3.ClampMagnitude(unclampedThrust, 1f);
                    __result = thrust;
                    return false;
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

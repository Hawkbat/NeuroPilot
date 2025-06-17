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

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.IsAutopilotAvailable))]
        public static bool ShipCockpitController_IsAutopilotAvailable(ref bool __result)
        {
            // Prevent player from activating autopilot directly
            __result = false;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.OnTargetReferenceFrame))]
        public static bool ShipCockpitController_OnTargetReferenceFrame()
        {
            // Prevent canceling of autopilot
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipCockpitController), nameof(ShipCockpitController.OnUntargetReferenceFrame))]
        public static bool ShipCockpitController_OnUntargetReferenceFrame()
        {
            // Prevent canceling of autopilot
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Locator), nameof(Locator.GetReferenceFrame))]
        public static bool Locator_GetReferenceFrame(ref ReferenceFrame __result)
        {
            // Override reference frame used as the target for landing mode, match velocity, etc.
            // Surely this will have no negative ramifications
            __result = EnhancedAutoPilot.GetInstance().GetLandingTargetReferenceFrame();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AlignShipWithReferenceFrame), nameof(AlignShipWithReferenceFrame.GetAlignmentDirection))]
        public static bool AlignShipWithReferenceFrame_GetAlignmentDirection(AlignShipWithReferenceFrame __instance, ref Vector3 __result)
        {
            // Same as vanilla but using the landing target reference frame
            var rf = EnhancedAutoPilot.GetInstance().GetLandingTargetReferenceFrame();
            if (rf == null)
            {
                __result = __instance._currentDirection;
                return false;
            }
            __result = rf.GetPosition() - __instance._owRigidbody.GetWorldCenterOfMass();
            return false;
        }
    }
}

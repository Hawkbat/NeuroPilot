using System.Collections.Generic;
using System.EnterpriseServices;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using NeuroPilot.Actions;
using NeuroSdk.Actions;
using UnityEngine;

namespace NeuroPilot
{

    [HarmonyPatch]
    public class ScoutPatches
    {
        public static HashSet<SurveyorProbe> surveyorProbes = [];
        public static Color surveyorProbeColor = Color.white;
        public static int surveyorProbeIntensity = 1;
        public static ProbeLauncher probeLauncher = null;   // Store the launcher to be able to take Snapshots

        [HarmonyPostfix, HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.LaunchProbe))]
        public static void ProbeLauncher_LaunchProbe_Postfix(ProbeLauncher __instance)
        {
            if (ModConfig.ScoutLauncher_Neuro)
            {
                NeuroActionHandler.RegisterActions(new TakeScoutPhotoAction(), new RetrieveScoutAction(), new SpinScoutAction(), new TurnScoutCameraAction());
            }
            NeuroActionHandler.UnregisterActions("launch_scout");
            probeLauncher = __instance;
            NeuroSdk.Messages.Outgoing.Context.Send("Scout launcher launched a scout");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.RetrieveProbe))]
        public static void ProbeLauncher_RetrieveProbe_Postfix()
        {
            NeuroActionHandler.UnregisterActions("take_scout_photo", "retrieve_scout", "spin_scout", "turn_scout_camera");
            NeuroSdk.Messages.Outgoing.Context.Send("Scout retrieved");
            if (probeLauncher.IsEquipped()) NeuroActionHandler.RegisterActions(new LauchScoutAction());
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SurveyorProbe), nameof(SurveyorProbe.Awake))]
        public static void SurveyorProbe_Awake_Postfix(SurveyorProbe __instance)
        {
            if (!ModConfig.ScoutLauncher_Neuro) return;
            if (surveyorProbes.Count == 0) NeuroActionHandler.RegisterActions(new SetScoutColorAction());
            surveyorProbes.Add(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.UpdatePostLaunch))]
        [HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.UpdatePreLaunch))]
        public static bool ProbeLauncher_UpdatePostLaunch_Prefix()
        {
            // Prevent manual probe control
            if (ModConfig.ScoutLauncher_Neuro && !ModConfig.ScoutLauncher_Manual)
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.EquipTool))]
        public static void SurveyorProbe_EquipTool_Postfix(ProbeLauncher __instance)
        {
            if (!ModConfig.ScoutLauncher_Neuro) return;
            probeLauncher = __instance;
            if (__instance.GetActiveProbe() == null) NeuroActionHandler.RegisterActions(new LauchScoutAction());
            NeuroSdk.Messages.Outgoing.Context.Send("Scout launcher equipped.");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.UnequipTool))]
        public static void SurveyorProbe_UnequipTool_Postfix()
        {
            if (!ModConfig.ScoutLauncher_Neuro) return;
            NeuroActionHandler.UnregisterActions("launch_scout");
            NeuroSdk.Messages.Outgoing.Context.Send("Scout launcher unequipped.");
        }

        public static void UpdateSurveyProbeLights()
        {
            foreach (SurveyorProbe probe in surveyorProbes)
            {
                if (probe == null) continue;
                foreach (OWLight2 light in probe.GetLights())
                {
                    light._light.color = surveyorProbeColor;
                    light.SetIntensity(surveyorProbeIntensity);

                    foreach (ProbeLantern lantern in probe.GetComponentsInChildren<ProbeLantern>())
                    {
                        if (lantern._emissiveRenderer == null) continue;
                        lantern._emissiveRenderer._renderer.sharedMaterial.SetColor(OWRenderer.s_propID_EmissionColor, ScoutPatches.surveyorProbeColor);
                    }
                }
            }
        }
        public static async UniTask TurnSurveyorProbeAsync(SurveyorProbe surveyorProbe, string direction, int steps)
        {
            float rotationStep = (direction == "left" || direction == "up") ? -30f : 30f;
            float duration = 2f * (steps / 12f); // Full rotation should take 2 seconds
            var rotatingCamera = surveyorProbe.GetRotatingCamera();

            float stepDuration = duration / steps;

            for (int i = 1; i <= steps; i++)
            {
                if (!surveyorProbe.IsAnchored()) return; // Handles retrieval during rotation
                if (direction == "left" || direction == "right")
                {
                    rotatingCamera.RotateHorizontal(rotationStep);
                }
                else
                {
                    rotatingCamera.RotateVertical(rotationStep);
                }

                probeLauncher.TakeSnapshotWithCamera(rotatingCamera);
                await UniTask.Delay((int)(stepDuration * 1000));
            }
        }
    }
}
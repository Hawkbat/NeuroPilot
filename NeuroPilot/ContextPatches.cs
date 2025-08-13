using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using HarmonyLib;
using NeuroSdk.Actions;
using UnityEngine;

namespace NeuroPilot
{

    [HarmonyPatch]
    public class ContextPatches
    {

        private static float _lastSignalTime = -10f;
        private static AudioSignal _lastSignal = null;

        /// <summary>
        /// This finalizer makes sure that any exceptions in the CharacterDialogueTree.DisplayDialogueBox2 Patch are suppressed.
        /// Otherwise, the dialogue would get stuck and the game would need to be restarted.
        /// </summary>
        [HarmonyFinalizer, HarmonyPatch(typeof(CharacterDialogueTree), nameof(CharacterDialogueTree.DisplayDialogueBox2))]
        public static System.Exception Finalizer(System.Exception __exception)
        {
            if (__exception == null) return null; // No exception, nothing to do
            NeuroPilot.instance.ModHelper.Console.WriteLine($"[NeuroScope] Exception in CharacterDialogueTree: {__exception.Message}\n{__exception.StackTrace}");
            return null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CharacterDialogueTree), nameof(CharacterDialogueTree.DisplayDialogueBox2))]
        public static void CharacterDialogueTree_DisplayDialogueBox2_Postfix(CharacterDialogueTree __instance, DialogueBoxVer2 __result)
        {
            SendContext("Dialogue Context", GetTextFromDialogue(__instance));
            if (!__result._displayedOptions.Any()) return;

            String optionsText = string.Join("\n", __result._displayedOptions.Select(option => option._text));
            SendContext("Dialogue Context", $"You can respond to {(__instance._characterName == "" ? "the NPC" : TextTranslation.Translate(__instance._characterName))} with the following options:\n" + optionsText);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NomaiText), nameof(NomaiText.SetAsTranslated))]
        public static void NomaiText_SetAsTranslated_Prefix(NomaiText __instance, int id)
        {
            __instance.VerifyInitialized();
            if (!__instance._dictNomaiTextData.ContainsKey(id)) return;
            if (__instance._dictNomaiTextData[id].IsTranslated) return;

            SendContext("Nomai Writing Context", $"Nomai Writing: {__instance._dictNomaiTextData[id].TextNode.InnerText}");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlayerCameraEffectController), nameof(PlayerCameraEffectController.WakeUp))]
        public static void PlayerCameraEffectController_WakeUp_Postfix()
        {
            SendContext("Death Context", $"Player woke up");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlayerCameraEffectController), nameof(PlayerCameraEffectController.OnPlayerDeath))]
        public static void PlayerCameraEffectController_OnPlayerDeath_Postfix(DeathType deathType)
        {
            string cause = $" - Cause: {deathType}";
            // Don't spoil the supernova in the first 10 loops
            if (deathType.Equals(DeathType.Default) || (deathType.Equals(DeathType.Supernova) && TimeLoop.GetLoopCount() <= 10))
            {
                cause = "";
            }
            SendContext("Death Context", $"Player died{cause}");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Steamworks.SteamUserStats), nameof(Steamworks.SteamUserStats.SetAchievement))]
        public static void SteamUserStats_SetAchievement_Prefix(string pchName)
        {
            Steamworks.SteamUserStats.GetAchievement(pchName, out bool achieved);
            if (achieved) return; // Make sure achievement is not already unlocked
            SendContext("Achievement Context", $"Achievement Unlocked: " +
                $"{Steamworks.SteamUserStats.GetAchievementDisplayAttribute(pchName, "name")} - " +
                $"{Steamworks.SteamUserStats.GetAchievementDisplayAttribute(pchName, "desc")}");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.PostNotification))]
        public static void NotificationManager_PostNotification_Prefix(NotificationManager __instance, NotificationData data)
        {
            if (!PlayerState.IsWearingSuit() || !PlayerState.IsInsideShip()) return; // Notifications are only shown when the player is wearing the spacesuit or inside the ship
            if (__instance._pinnedNotifications.Contains(data)) return; // Don't send duplicate notifications
            SendContext("Notification Context", $"Notification: {data.displayMessage}");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Sector), nameof(Sector.OnEntry))]
        public static void Sector_OnEntry_Prefix(Sector __instance, GameObject hitObj)
        {
            if (__instance._name == Sector.Name.QuantumMoon) return; // Prevent spoiling the Quantum Moon
            SectorDetector component = hitObj.GetComponent<SectorDetector>();
            if (component == null) return;
            if (component.GetOccupantType() != DynamicOccupant.Player) return;
            string sectorName = SectorToString(__instance);
            if (sectorName == null) return;
            SendContext("Location Context", $"Player entered {sectorName}");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Sector), nameof(Sector.OnExit))]
        public static void Sector_OnExit_Prefix(Sector __instance, GameObject hitObj)
        {
            if (PlayerState.IsDead()) return;
            if (__instance._name == Sector.Name.QuantumMoon) return; // Prevent spoiling the Quantum Moon
            SectorDetector component = hitObj.GetComponent<SectorDetector>();
            if (component == null) return;
            if (component.GetOccupantType() != DynamicOccupant.Player) return;
            string sectorName = SectorToString(__instance);
            if (sectorName == null) return;
            SendContext("Location Context", $"Player left {sectorName}");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ShipLogFact), nameof(ShipLogFact.Reveal))]
        public static void ShipLogFact_Reveal_Prefix(ShipLogFact __instance)
        {
            if (__instance.IsRevealed()) return; // Make sure we only send the fact once
            ShipLogEntry shipLogEntry = Locator.GetShipLogManager().GetEntry(__instance._entryID);
            string astroObjectName = AstroObject.AstroObjectNameToString(AstroObject.StringIDToAstroObjectName(shipLogEntry._astroObjectID));
            SendContext("Ship Log Context", $"[NEW SHIP LOG FACT] {astroObjectName} - {shipLogEntry.GetName(false)}: {__instance.GetText()}");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SignalscopeUI), nameof(SignalscopeUI.UpdateLabels))]
        public static void SignalscopeUI_UpdateLabels_Postfix(SignalscopeUI __instance)
        {
            AudioSignal strongestSignal = __instance._signalscopeTool.GetStrongestSignal();
            if (_lastSignal == strongestSignal && Time.time - _lastSignalTime < 10f) return;
            if (strongestSignal == null || strongestSignal.GetSignalStrength() < 0.9f) return;
            string text = PlayerData.KnowsSignal(strongestSignal.GetName()) ? AudioSignal.SignalNameToString(strongestSignal.GetName()) : UITextLibrary.GetString(UITextType.UnknownSignal);
            SendContext("Signalscope Context", $"Listening to Signal: '{text}' | Distance: {Mathf.Round(strongestSignal.GetDistanceFromScope())}m | Frequency: '{AudioSignal.FrequencyToString(strongestSignal._frequency, false)}'");
            _lastSignalTime = Time.time;
            _lastSignal = strongestSignal;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GlobalMessenger), nameof(GlobalMessenger.FireEvent))]
        public static void GlobalMessenger_FireEvent_Postfix(string eventType)
        {
            switch (eventType)
            {
                case "PlayerEnterQuantumMoon":
                    SendContext("Location Context", "Player entered the Quantum Moon");
                    break;
                case "PlayerExitQuantumMoon":
                    SendContext("Location Context", "Player left the Quantum Moon");
                    break;
                case "PlayerEnterBlackHole":
                    SendContext("Location Context", "Player entered a Black Hole");
                    break;
                case "PlayerEscapedTimeLoop":
                    SendContext("Death Context", "Player escaped the time loop");
                    break;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Campfire), nameof(Campfire.StartRoasting))]
        public static void Campfire_StartRoasting_Postfix()
        {
            SendContext("Misc Context", $"Player started roasting a marshmallow");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Marshmallow), nameof(Marshmallow.Eat))]
        public static void Marshmallow_Eat_Prefix(Marshmallow __instance)
        {
            SendContext("Misc Context", $"Player ate a {(__instance.IsBurned() ? "burned " : "")}marshmallow");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Campfire), nameof(Campfire.StartSleeping))]
        public static void Campfire_StartSleeping_Postfix()
        {
            SendContext("Misc Context", $"Player started sleeping at a campfire");
        }

        private static string GetTextFromDialogue(CharacterDialogueTree characterDialogueTree)
        {
            string text = characterDialogueTree._currentNode._name + characterDialogueTree._currentNode._listPagesToDisplay[characterDialogueTree._currentNode._currentPage];
            text = TextTranslation.Translate(text).Trim();
            if (characterDialogueTree._characterName == "SIGN")
            {
                text = $"Sign reads: {text}";
            }
            else if (characterDialogueTree._characterName != "")
            {
                text = $"{TextTranslation.Translate(characterDialogueTree._characterName)} says: {text}";
            }
            return StripHtml(text);
        }

        private static string StripHtml(string text)
        {
            return Regex.Replace(text, "<.*?>", string.Empty);
        }

        private static void SendContext(string settingsKey, string text)
        {
            if (ModConfig.Get<string>(settingsKey).Equals("Disabled")) return;
            NeuroSdk.Messages.Outgoing.Context.Send(StripHtml(text), !ModConfig.Get<string>(settingsKey).Equals("Enabled"));
        }

        private static string SectorToString(Sector sector)
        {
            return sector.GetName() switch
            {
                Sector.Name.Sun => null,// Sun Sector is too big
                Sector.Name.HourglassTwin_A => null,// Already handled by Sector.Name.HourglassTwins
                Sector.Name.HourglassTwin_B => null,// Already handled by Sector.Name.HourglassTwins
                Sector.Name.TimberHearth => UITextLibrary.GetString(UITextType.LocationTH),
                Sector.Name.BrittleHollow => UITextLibrary.GetString(UITextType.LocationBH),
                Sector.Name.GiantsDeep => UITextLibrary.GetString(UITextType.LocationGD),
                Sector.Name.DarkBramble => UITextLibrary.GetString(UITextType.LocationDB),
                Sector.Name.Comet => UITextLibrary.GetString(UITextType.LocationCo),
                Sector.Name.QuantumMoon => UITextLibrary.GetString(UITextType.LocationQM),
                Sector.Name.TimberMoon => UITextLibrary.GetString(UITextType.LocationTHMoon),
                Sector.Name.BrambleDimension => UITextLibrary.GetString(UITextType.LocationDB) + " Interior",
                Sector.Name.VolcanicMoon => UITextLibrary.GetString(UITextType.LocationBHMoon),
                Sector.Name.OrbitalProbeCannon => UITextLibrary.GetString(UITextType.LocationOPC),
                Sector.Name.EyeOfTheUniverse => UITextLibrary.GetString(UITextType.LocationEye),
                Sector.Name.Ship => null,// Already sent by autopilot
                Sector.Name.SunStation => UITextLibrary.GetString(UITextType.LocationSS),
                Sector.Name.WhiteHole => UITextLibrary.GetString(UITextType.LocationWH),
                Sector.Name.TimeLoopDevice => null,
                Sector.Name.Vessel => "The Vessel",
                Sector.Name.VesselDimension => "The Vessel",
                Sector.Name.HourglassTwins => UITextLibrary.GetString(UITextType.LocationHGT),
                Sector.Name.InvisiblePlanet => UITextLibrary.GetString(UITextType.LocationIP),
                Sector.Name.DreamWorld => null,
                Sector.Name.Unnamed => null,
                _ => null,
            };
        }
    }
}
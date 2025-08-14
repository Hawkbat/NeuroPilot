using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroPilot
{
    public static class Locations
    {
        static Transform _mapSatellite = null;

        public static Transform GetMapSatellite()
        {
            if (!_mapSatellite)
                _mapSatellite = GameObject.Find("HearthianMapSatellite_Body")?.transform;

            return _mapSatellite;
        }

        public static IEnumerable<(string, Transform)> ByDestination(Destination destination)
        {
            if (destination == Destinations.GetPlayerLocation())
            {
                var playerDestination = Destinations.GetByType<PlayerDestination>();
                yield return (playerDestination.Name, playerDestination.GetTransform());
            }

            var destinationRoot = destination.GetTransform();
            yield return (destination.Name, destinationRoot);

            foreach (var entryLocation in Locator._entryLocationsByID.Values.Where(e => e.transform.root == destination.GetTransform() || Vector3.Distance(e.GetPosition(), destination.GetReferenceFrame().GetPosition()) < destination.InnerRadius))
            {
                var entry = Locator.GetShipLogManager().GetEntry(entryLocation.GetEntryID());
                var entryName = entry.GetName(false);

                if (entry.GetState() != ShipLogEntry.State.Explored)
                    continue;

                // Dark Bramble locations point to Dark Bramble itself if outside and otherwise are invalid autopilot locations
                if (entryLocation.GetOuterFogWarpVolume() != null)
                {
                    var shipInBramble = Locator.GetShipDetector().GetComponent<FogWarpDetector>().GetOuterFogWarpVolume() != null;
                    if (!shipInBramble)
                    {
                        yield return (entryName, Locator.GetAstroObject(AstroObject.Name.DarkBramble).transform);
                    }
                    continue;
                }

                // Locations in the Stranger are invalid if outside the cloaking field
                if (entryLocation.IsWithinCloakField() && !Locator.GetCloakFieldController().isShipInsideCloak)
                {
                    continue;
                }

                yield return (entryName, entryLocation.GetTransform());
            }
        }

        public static (string, Transform) ByName(string name, out string error)
        {
            name = name.ToUpper().Trim();

            var playerDestination = Destinations.GetByType<PlayerDestination>();
            if (name == "PLAYER" || name == playerDestination.Name.ToUpper())
            {
                error = string.Empty;
                return (playerDestination.Name, playerDestination.GetTransform());
            }

            var destination = Destinations.GetByName(name);
            if (destination != null)
            {
                error = string.Empty;
                return (destination.Name, destination.GetTransform());
            }

            var entry = Locator.GetShipLogManager().GetEntryList().Find(e => e.GetName(false).ToUpper().Replace("THE ", "").Trim() == name);
            if (entry != null)
            {
                var entryName = entry.GetName(false);

                if (entry.GetState() != ShipLogEntry.State.Explored)
                {
                    error = "Location has not been discovered yet.";
                    return (entryName, null);
                }

                var entryLocation = Locator.GetEntryLocation(entry.GetID());
                if (entryLocation == null)
                {
                    error = "Location exists but has no known position.";
                    return (entryName, null);
                }


                // Dark Bramble locations point to Dark Bramble itself if outside and otherwise are invalid autopilot locations
                if (entryLocation.GetOuterFogWarpVolume() != null)
                {
                    var shipInBramble = Locator.GetShipDetector().GetComponent<FogWarpDetector>().GetOuterFogWarpVolume() != null;
                    if (shipInBramble)
                    {
                        error = "Location exists but spatial distortions prevent autopilot from navigating in Dark Bramble.";
                        return (entryName, null);
                    }

                    error = string.Empty;
                    return (entryName, Locator.GetAstroObject(AstroObject.Name.DarkBramble).transform);
                }

                // Locations in the Stranger (except for the Damaged Laboratory) always point to the first valid hangar
                if (entryLocation.IsWithinCloakField() && entryName != "Damaged Laboratory")
                {
                    var strangerDestination = Destinations.GetByType<StrangerDestination>();
                    error = string.Empty;

                    // Fallback if DLC isn't installed
                    if (strangerDestination == null || !strangerDestination.IsAvailable(out error))
                    {
                        error ??= "No matching location found.";
                        return (entryName, null);
                    }

                    return (entryName, strangerDestination.GetTransform());
                }

                error = string.Empty;
                return (entry.GetName(false), entryLocation.GetTransform());
            }

            // If no location found by this point, try again without the "THE "
            if (name.StartsWith("THE "))
                return ByName(name.Substring("THE ".Length), out error);

            error = "No matching location found.";
            return (null, null);
        }

        public static ReferenceFrameVolume AddReferenceFrame(GameObject obj, float radius, float minTargetRadius, float maxTargetRadius)
        {
            var go = new GameObject("RFVolume");
            obj.GetAttachedOWRigidbody().SetIsTargetable(false);
            go.transform.SetParent(obj.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.layer = LayerMask.NameToLayer("ReferenceFrameVolume");
            go.SetActive(false);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0f;

            var rfv = go.AddComponent<ReferenceFrameVolume>();
            rfv._referenceFrame = new ReferenceFrame(obj.GetComponent<OWRigidbody>())
            {
                _minSuitTargetDistance = minTargetRadius,
                _maxTargetDistance = maxTargetRadius,
                _autopilotArrivalDistance = radius,
                _autoAlignmentDistance = radius * 0.75f,
                _hideLandingModePrompt = false,
                _matchAngularVelocity = true,
                _minMatchAngularVelocityDistance = 70,
                _maxMatchAngularVelocityDistance = 400,
                _bracketsRadius = radius * 0.5f,
                _useCenterOfMass = false,
                _localPosition = Vector3.zero,
            };

            rfv._minColliderRadius = minTargetRadius;
            rfv._maxColliderRadius = radius;
            rfv._isPrimaryVolume = false;
            rfv._isCloseRangeVolume = false;

            go.SetActive(true);
            return rfv;
        }
    }
}

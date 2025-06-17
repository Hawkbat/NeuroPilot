using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroPilot
{
    public static class Destinations
    {
        static readonly Destination playerDestination = new("Player", string.Empty);
        static readonly Destination strangerDarkSideDestination = new("The Stranger (Dark Side Docking Bay)", "SolarSystemRoot/RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_DarkSideDockingBay");
        static readonly Destination strangerLightSideDestination = new("The Stranger (Light Side Docking Bay)", "SolarSystemRoot/RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_LightSideDockingBay");

        static readonly Destination[] destinations = [
            playerDestination,
            strangerDarkSideDestination,
            strangerLightSideDestination,
            
            new Destination("The Sun", "SolarSystemRoot/Sun_Body/RFVolume_SUN"),
            new Destination("Sun Station", "SolarSystemRoot/SunStation_Pivot/SunStation_Body/Sector_SunStation/Sector_WarpModule/Volumes_WarpModule/RFVolume"),
            
            new Destination("Ash Twin", "SolarSystemRoot/FocalBody/TowerTwin_Body/Volumes_TowerTwin/RFVolume"),
            new Destination("Ember Twin", "SolarSystemRoot/FocalBody/CaveTwin_Body/Volumes_CaveTwin/RFVolume"),
            
            new Destination("Timber Hearth", "SolarSystemRoot/TimberHearth_Body/RFVolume_TH"),
            new Destination("The Attlerock", "SolarSystemRoot/Moon_Pivot/Moon_Body/RFVolume_THM"),
            
            new Destination("Brittle Hollow", "SolarSystemRoot/BrittleHollow_Body/RFVolume_BH"),
            new Destination("Hollow's Lantern", "SolarSystemRoot/VolcanicMoon_Pivot/VolcanicMoon_Body/RFVolume_VM"),
            
            new Destination("Giant's Deep", "SolarSystemRoot/GiantsDeep_Body/RFVolume_GD"),
            new Destination("Orbital Probe Cannon", "SolarSystemRoot/OrbitalProbeCannon_Pivot/OrbitalProbeCannon_Body/RFVolume_OrbitalProbeCannon"),
            //new Destination("Orbital Probe Cannon (Control Module)", "SolarSystemRoot/SunStation_Pivot/SunStation_Body/Sector_SunStation/Sector_ControlModule/Volumes/RFVolume"),
            
            new Destination("Dark Bramble", "SolarSystemRoot/DarkBramble_Body/RFVolume_DB"),
            
            new Destination("The Interloper", "SolarSystemRoot/Comet_Body/RFVolume_CO"),
            //new Destination("Interloper Shuttle", "SolarSystemRoot/Comet_Body/Prefab_NOM_Shuttle/Shuttle_Body/RF_Volume"),
            
            new Destination("White Hole Station", "SolarSystemRoot/WhiteHole_Body/WhiteholeStation_Body/RFVolume_WhiteholeStation"),
            
            new Destination("Hearthian Map Satellite", "SolarSystemRoot/HearthianMapSatellite_PivotY/HearthianMapSatellite_PivotX/HearthianMapSatellite_Body/RFVolume_HMS"),
            
            new Destination("The Quantum Moon", "SolarSystemRoot/QuantumMoon_Body/Volumes/RFVolume"),
            //new Destination("Quantum Moon Shuttle", "SolarSystemRoot/QuantumMoon_Body/Sector_QuantumMoon/QuantumShuttle/Prefab_NOM_Shuttle/Shuttle_Body/RF_Volume"),

            new Destination("Backer Satellite", "SolarSystemRoot/BackerSatellite_Pivot/BackerSatellite_Body/RFVolume_BS"),
            
            //new Destination("Player's Ship", "SolarSystemRoot/TimberHearth_Body/ShipContainer/Ship_Body/Volumes/RFVolume"),
        ];

        static ReferenceFrameVolume playerRFV;

        public static IEnumerable<Destination> GetAll()
        {
            foreach (var dest in destinations)
            {
                yield return dest;
            }
        }

        public static IEnumerable<string> GetAllNames() => GetAll().Select(d => d.name);

        public static Destination GetByName(string name)
        {
            return GetAll().FirstOrDefault(d => d.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static Destination GetByReferenceFrame(ReferenceFrame rf)
        {
            if (rf == null) return null;
            foreach (var dest in GetAll())
            {
                var destRF = dest.GetReferenceFrame();
                if (destRF != null && destRF == rf)
                {
                    return dest;
                }
            }
            return null;
        }

        public static void SetUp()
        {
            foreach (var d in destinations) d.GetReferenceFrame();
            if (!playerRFV)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                
                var go = new GameObject("RFVolume");
                go.transform.SetParent(player.transform, false);
                go.layer = LayerMask.NameToLayer("ReferenceFrameVolume");
                go.SetActive(false);

                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0f;

                playerRFV = go.AddComponent<ReferenceFrameVolume>();
                playerRFV._referenceFrame = new ReferenceFrame(player.GetComponent<OWRigidbody>())
                {
                    _autopilotArrivalDistance = 20f,
                    _maxTargetDistance = 0f,
                    _useCenterOfMass = false,
                };

                go.SetActive(true);
            }
            playerDestination.SetReferenceFrameVolume(playerRFV);
        }
    }

    public class Destination(string name, string path)
    {
        public readonly string name = name;
        public readonly string path = path;

        public override string ToString() => name;

        ReferenceFrameVolume rfv;

        public ReferenceFrameVolume GetReferenceFrameVolume()
        {
            if (!rfv && !string.IsNullOrEmpty(path))
                rfv = GameObject.Find(path).GetComponent<ReferenceFrameVolume>();
            return rfv;
        }

        public void SetReferenceFrameVolume(ReferenceFrameVolume rfv)
        {
            this.rfv = rfv;
        }

        public ReferenceFrame GetReferenceFrame()
        {
            var rfv = GetReferenceFrameVolume();
            if (rfv) return rfv.GetReferenceFrame();
            return null;
        }

        public float GetDistanceToShip()
        {
            var shipPos = Locator.GetShipBody().GetPosition();
            var destPos = rfv ? rfv.GetReferenceFrame().GetPosition() : shipPos;
            return Vector3.Distance(destPos, shipPos);
        }

        public float GetDistanceToPlayer()
        {
            var playerPos = Locator.GetPlayerBody().GetPosition();
            var destPos = rfv ? rfv.GetReferenceFrame().GetPosition() : playerPos;
            return Vector3.Distance(destPos, playerPos);
        }
    }
}

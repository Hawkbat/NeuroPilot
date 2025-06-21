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
        static readonly List<Destination> destinations = [
            new PlayerDestination(),

            new StrangerDestination("The Stranger (Dark Side Docking Bay)", "SolarSystemRoot/RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_DarkSideDockingBay", false, 200f, 300f),
            new StrangerDestination("The Stranger (Light Side Docking Bay)", "SolarSystemRoot/RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_LightSideDockingBay", true, 200f, 300f),

            new FixedDestination("The Sun", "SolarSystemRoot/Sun_Body/RFVolume_SUN", 25000f, 25000f),
            new FixedDestination("Sun Station (Warp Module)", "SolarSystemRoot/SunStation_Pivot/SunStation_Body/Sector_SunStation/Sector_WarpModule/Volumes_WarpModule/RFVolume", 50f, 150f),
            new FixedDestination("Sun Station (Control Module)", "SolarSystemRoot/SunStation_Pivot/SunStation_Body/Sector_SunStation/Sector_ControlModule/Volumes/RFVolume", 50f, 150f),

            new LandingDestination("Ash Twin", "SolarSystemRoot/FocalBody/TowerTwin_Body/Volumes_TowerTwin/RFVolume", 380f, 600f),
            new LandingDestination("Ember Twin", "SolarSystemRoot/FocalBody/CaveTwin_Body/Volumes_CaveTwin/RFVolume", 380f, 600f),

            new LandingDestination("Timber Hearth", "SolarSystemRoot/TimberHearth_Body/RFVolume_TH", 400f, 700f),
            new LandingDestination("The Attlerock", "SolarSystemRoot/Moon_Pivot/Moon_Body/RFVolume_THM", 130f, 300f),

            new LandingDestination("Brittle Hollow", "SolarSystemRoot/BrittleHollow_Body/RFVolume_BH", 600f, 900f),
            new FixedDestination("Hollow's Lantern", "SolarSystemRoot/VolcanicMoon_Pivot/VolcanicMoon_Body/RFVolume_VM", 200f, 500f),

            new LandingDestination("Giant's Deep", "SolarSystemRoot/GiantsDeep_Body/RFVolume_GD", 950f, 1500f),
            new FixedDestination("Orbital Probe Cannon", "SolarSystemRoot/OrbitalProbeCannon_Pivot/OrbitalProbeCannon_Body/RFVolume_OrbitalProbeCannon", 200f, 400f),
            
            new FixedDestination("Dark Bramble", "SolarSystemRoot/DarkBramble_Body/RFVolume_DB", 950f, 1400f),

            new LandingDestination("The Interloper", "SolarSystemRoot/Comet_Body/RFVolume_CO", 300f, 600f),
            new ShuttleDestination("Interloper Shuttle", "SolarSystemRoot/Comet_Body/Prefab_NOM_Shuttle/Shuttle_Body/RF_Volume", 50f, 100f),
            
            new FixedDestination("White Hole Station", "SolarSystemRoot/WhiteHole_Body/WhiteholeStation_Body/RFVolume_WhiteholeStation", 100f, 300f),

            new FixedDestination("Hearthian Map Satellite", "SolarSystemRoot/HearthianMapSatellite_PivotY/HearthianMapSatellite_PivotX/HearthianMapSatellite_Body/RFVolume_HMS", 100f, 300f),

            new LandingDestination("The Quantum Moon", "SolarSystemRoot/QuantumMoon_Body/Volumes/RFVolume", 110f, 300f),
            new ShuttleDestination("Quantum Moon Shuttle", "SolarSystemRoot/QuantumMoon_Body/Sector_QuantumMoon/QuantumShuttle/Prefab_NOM_Shuttle/Shuttle_Body/RF_Volume", 50f, 100f),

            new FixedDestination("Backer Satellite", "SolarSystemRoot/BackerSatellite_Pivot/BackerSatellite_Body/RFVolume_BS", 100f, 300f),
            
            new ShipDestination(),
        ];

        public static IEnumerable<Destination> GetAll() => destinations;
        public static IEnumerable<Destination> GetAllValid() => destinations.Where(d => d.IsAvailable(out string reason));

        public static IEnumerable<string> GetAllValidNames() => GetAllValid().Select(d => d.GetName());

        public static Destination GetByName(string name)
            => GetAllValid().Concat(GetAll()).FirstOrDefault(d => d.GetName().Equals(name, StringComparison.OrdinalIgnoreCase));

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

        public static Destination GetShipLocation()
            => GetAllValid().Where(d => d.ShipIsAt()).OrderBy(d => d.GetDistanceToShip()).FirstOrDefault();
        public static Destination GetPlayerLocation()
            => GetAllValid().Where(d => d.PlayerIsAt()).OrderBy(d => d.GetDistanceToPlayer()).FirstOrDefault();

        public static void SetUp()
        {
            foreach (var d in destinations) d.GetReferenceFrame();
        }
    }

    public abstract class Destination(string name, float innerRadius, float outerRadius)
    {
        protected readonly string name = name;
        protected readonly float innerRadius = innerRadius;
        protected readonly float outerRadius = outerRadius;

        public virtual string GetName() => name;
        public float GetInnerRadius() => innerRadius;
        public float GetOuterRadius() => outerRadius;

        public override string ToString() => GetName();

        public abstract bool CanLand();

        public abstract ReferenceFrame GetReferenceFrame();

        public virtual bool IsAvailable(out string reason)
        {
            if (GetReferenceFrame() == null)
            {
                reason = "Destination does not currently exist.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public virtual bool ShipIsAt() => GetDistanceToShip() <= outerRadius;
        public virtual bool PlayerIsAt() => GetDistanceToPlayer() <= outerRadius;

        public abstract float GetDistanceToShip();
        public abstract float GetDistanceToPlayer();
    }

    public class FixedDestination(string name, string path, float innerRadius, float outerRadius) : Destination(name, innerRadius, outerRadius)
    {
        protected readonly string path = path;

        ReferenceFrameVolume rfv;

        public override bool CanLand() => false;

        public ReferenceFrameVolume GetReferenceFrameVolume()
        {
            if (!rfv && !string.IsNullOrEmpty(path))
                rfv = GameObject.Find(path).GetComponent<ReferenceFrameVolume>();
            return rfv;
        }

        public override ReferenceFrame GetReferenceFrame()
        {
            var rfv = GetReferenceFrameVolume();
            if (rfv) return rfv.GetReferenceFrame();
            return null;
        }

        public override float GetDistanceToShip()
        {
            var shipPos = Locator.GetShipBody().GetPosition();
            var destPos = rfv ? rfv.GetReferenceFrame().GetPosition() : shipPos;
            return Vector3.Distance(destPos, shipPos);
        }

        public override float GetDistanceToPlayer()
        {
            var playerPos = Locator.GetPlayerBody().GetPosition();
            var destPos = rfv ? rfv.GetReferenceFrame().GetPosition() : playerPos;
            return Vector3.Distance(destPos, playerPos);
        }
    }

    public class LandingDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override bool CanLand() => true;
    }

    public class StrangerDestination(string name, string path, bool lightSide, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        readonly bool lightSide = lightSide;

        public override string GetName()
        {
            if (!Locator.GetShipLogManager() || !Locator.GetShipLogManager().IsFactRevealed("IP_RING_WORLD_X1"))
            {
                return "Dark shadow over the sun";
            }
            return "The Stranger";
        }

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            var ringWorld = Locator.GetAstroObject(AstroObject.Name.RingWorld).transform;
            var sun = Locator.GetAstroObject(AstroObject.Name.Sun).transform;
            var ship = Locator.GetShipTransform();

            if (!ringWorld || !sun || !ship)
            {
                reason = "Game not loaded yet.";
                return false;
            }

            var eclipseDot = Vector3.Dot((sun.position - ship.position).normalized, (ringWorld.position - ship.position).normalized);

            // If we have not discovered the Stranger yet, check if the ship is near the map satellite and the eclipse is visible
            if (!Locator.GetShipLogManager() || !Locator.GetShipLogManager().IsFactRevealed("IP_RING_WORLD_X1"))
            {
                var mapSatellite = Locator.GetAstroObject(AstroObject.Name.MapSatellite).transform;
                var mapSatelliteDistance = Vector3.Distance(ship.position, mapSatellite.position);
                var isEclipseVisible = eclipseDot > 0.99f;
                var isNearMapSatellite = mapSatelliteDistance < 100f;

                if (!isNearMapSatellite)
                {
                    reason = "Ship is not near the Hearthian Map Satellite.";
                    return false;
                }

                if (!isEclipseVisible)
                {
                    reason = "Eclipse is not visible from the ship's position.";
                    return false;
                }
            }

            // Only the destination on the same side of the Stranger as the ship is valid
            var isOnLightSide = eclipseDot < 0f;

            if (isOnLightSide != lightSide)
            {
                reason = $"Ship is on the {(isOnLightSide ? "light" : "dark")} side of the Stranger, but this destination is on the {(lightSide ? "light" : "dark")} side.";
                return false;
            }

            return true;
        }
    }

    public class ShuttleDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        NomaiShuttleController controller;

        public override bool CanLand() => false;
        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;
            if (!controller)
            {
                var shuttleBody = GetReferenceFrame()?.GetOWRigidBody();
                if (shuttleBody)
                {
                    controller = shuttleBody.GetComponentInChildren<NomaiShuttleController>();   
                }
                if (!controller)
                {
                    reason = "Shuttle not found.";
                    return false;
                }
            }
            if (!controller.IsPlayerInside())
            {
                reason = "Player is not currently inside the shuttle.";
                return false;
            }
            return true;
        }
    }

    public class ShipDestination() : FixedDestination("Ship", "SolarSystemRoot/TimberHearth_Body/ShipContainer/Ship_Body/Volumes/RFVolume", 0f, 20f)
    {
        public override bool CanLand() => false;

        public override bool IsAvailable(out string reason)
        {
            reason = "Ship cannot autopilot to itself.";
            return false;
        }

        public override bool PlayerIsAt() => PlayerState.IsInsideShip();
        public override bool ShipIsAt() => false;
        public override float GetDistanceToPlayer() => Vector3.Distance(Locator.GetPlayerBody().GetPosition(), Locator.GetShipBody().GetPosition());
        public override float GetDistanceToShip() => 0f;
    }

    public class PlayerDestination() : Destination("Player", 0f, 0f)
    {
        ReferenceFrameVolume rfv;

        public override string GetName() => StandaloneProfileManager.SharedInstance.currentProfile?.profileName ?? "Player";

        public override bool CanLand() => false;

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;
            if (PlayerState.IsInsideShip())
            {
                reason = "Player is inside the ship.";
                return false;
            }
            return true;
        }

        public override ReferenceFrame GetReferenceFrame()
        {
            if (!rfv)
            {
                var player = GameObject.FindGameObjectWithTag("Player");

                var go = new GameObject("RFVolume");
                go.transform.SetParent(player.transform, false);
                go.layer = LayerMask.NameToLayer("ReferenceFrameVolume");
                go.SetActive(false);

                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0f;

                rfv = go.AddComponent<ReferenceFrameVolume>();
                rfv._referenceFrame = new ReferenceFrame(player.GetComponent<OWRigidbody>())
                {
                    _autopilotArrivalDistance = 20f,
                    _maxTargetDistance = 0f,
                    _useCenterOfMass = false,
                };

                go.SetActive(true);
            }
            return rfv.GetReferenceFrame();
        }

        public override bool PlayerIsAt() => false;
        public override bool ShipIsAt() => false;

        public override float GetDistanceToPlayer() => 0f;

        public override float GetDistanceToShip() => Vector3.Distance(Locator.GetPlayerBody().GetPosition(), Locator.GetShipBody().GetPosition());
    }
}

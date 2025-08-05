using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroPilot
{
    public static class Destinations
    {
        static readonly List<Destination> destinations = [
            new PlayerDestination(),

            new StrangerDestination("The Stranger (Dark Side Docking Bay)", "RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_DarkSideDockingBay", false, 200f, 300f),
            new StrangerDestination("The Stranger (Light Side Docking Bay)", "RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_LightSideDockingBay", true, 200f, 300f),

            new FloatingDestination("The Sun", "Sun_Body/RFVolume_SUN", 3000f, 4000f),
            new SunStationDestination("Sun Station (Warp Module)", "SunStation_Body/Sector_SunStation/Sector_WarpModule/Volumes_WarpModule/RFVolume", 50f, 150f),
            new SunStationDestination("Sun Station (Control Module)", "SunStation_Body/Sector_SunStation/Sector_ControlModule/Volumes/RFVolume", 50f, 150f),

            new PlanetoidDestination("Ash Twin", "TowerTwin_Body/Volumes_TowerTwin/RFVolume", 380f, 1000f),
            new PlanetoidDestination("Ember Twin", "CaveTwin_Body/Volumes_CaveTwin/RFVolume", 380f, 1000f),

            new PlanetoidDestination("Timber Hearth", "TimberHearth_Body/RFVolume_TH", 400f, 1000f),
            new PlanetoidDestination("The Attlerock", "Moon_Body/RFVolume_THM", 130f, 350f),

            new PlanetoidDestination("Brittle Hollow", "BrittleHollow_Body/RFVolume_BH", 600f, 1000f),
            new FloatingDestination("Hollow's Lantern", "VolcanicMoon_Body/RFVolume_VM", 200f, 500f),

            new PlanetoidDestination("Giant's Deep", "GiantsDeep_Body/RFVolume_GD", 950f, 2500f),
            new OPCDestination("Orbital Probe Cannon", "OrbitalProbeCannon_Body/RFVolume_OrbitalProbeCannon", 200f, 400f),
            new ProbeDestination("Probe", "NomaiProbe_Body/RFVolume", 100f, 500f),

            new FloatingDestination("Dark Bramble", "DarkBramble_Body/RFVolume_DB", 950f, 1800f),

            new PlanetoidDestination("The Interloper", "Comet_Body/RFVolume_CO", 300f, 600f),
            new ShuttleDestination("Ember Twin Shuttle", NomaiShuttleController.ShuttleID.HourglassShuttle, 50f, 200f),
            
            new WhiteHoleStationDestination("White Hole Station", "WhiteholeStation_Body/RFVolume_WhiteholeStation", 100f, 300f),

            new MapSatelliteDestination("Hearthian Map Satellite", "HearthianMapSatellite_Body/RFVolume_HMS", 100f, 300f),

            new QuantumMoonDestination("The Quantum Moon", "QuantumMoon_Body/Volumes/RFVolume", 110f, 500f),
            new ShuttleDestination("Brittle Hollow Shuttle", NomaiShuttleController.ShuttleID.BrittleHollowShuttle, 50f, 200f),

            new FloatingDestination("Secret Satellite", "BackerSatellite_Body/RFVolume_BS", 200f, 400f),
            
            new ShipDestination(),
            
            new TargetedDestination()
        ];

        public static IEnumerable<Destination> GetAll() => destinations;
        public static IEnumerable<Destination> GetAllValid() => destinations.Where(d => d.IsAvailable(out string reason));

        public static void UpdateNames() => destinations.ForEach(d => d.UpdateName());
        public static IEnumerable<string> GetAllNames() => destinations.Select(d => d.GetName());
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

        public static T GetByType<T>() where T : class => GetAllValid().Concat(GetAll()).OfType<T>().FirstOrDefault();

        public static Destination GetShipLocation()
            => GetAllValid().Where(d => d.ShipIsAt()).OrderBy(d => d.GetDistanceToShip()).FirstOrDefault();
        public static Destination GetPlayerLocation()
            => GetAllValid().Where(d => d.PlayerIsAt()).OrderBy(d => d.GetDistanceToPlayer()).FirstOrDefault();

        public static void SetUp()
        {
            foreach (var d in destinations) d.SetUp();
            Destinations.UpdateNames();
        }
    }

    public abstract class Destination(string name, float innerRadius, float outerRadius)
    {
        protected string name = name;
        protected readonly float innerRadius = innerRadius;
        protected readonly float outerRadius = outerRadius;

        public virtual string GetName() => name;
        public float GetInnerRadius() => innerRadius;
        public float GetOuterRadius() => outerRadius;

        public virtual string GetNewName() => GetName();
        public void UpdateName() => name = GetNewName();

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

        public virtual void SetUp() { }
    }

    public abstract class FixedDestination(string name, string path, float innerRadius, float outerRadius) : Destination(name, innerRadius, outerRadius)
    {
        protected readonly string path = path;

        ReferenceFrameVolume rfv;

        public override bool CanLand() => false;

        public override ReferenceFrame GetReferenceFrame()
        {
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

        public override void SetUp()
        {
            var parts = path.Split('/');
            var go = GameObject.Find(parts[0]);
            if (go == null)
            {
                NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{name}' at path '{path}' does not exist. Missing part: {parts[0]}", MessageType.Warning);
                return;
            }
            for (var i = 1; i < parts.Length; i++)
            {
                var t = go.transform.Find(parts[i]);
                if (t == null)
                {
                    NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{name}' at path '{path}' does not exist. Missing part: {parts[i]}", MessageType.Warning);
                    return;
                }
                go = t.gameObject;
            }

            rfv = go.GetComponent<ReferenceFrameVolume>();
            if (!rfv) NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{name}' at path '{path}' does not have a ReferenceFrameVolume component.", MessageType.Warning);
        }
    }

    public class FloatingDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {

    }

    public class ProbeDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;
            if (Locator.GetShipBody() != null && GetDistanceToShip() > 50_000f)
            {
                reason = $"{GetName()} is too far.";
                return false;
            }
            return true;
        }

        public override string GetNewName()
        {
            if (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("ORBITAL_PROBE_CANNON").GetState() == ShipLogEntry.State.Hidden)
            {
                return "Fired blue thing";
            }
            return "Probe";
        }
    }

    public class PlanetoidDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override bool CanLand() => true;
    }

    public class SunStationDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string GetNewName()
        {
            if (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("S_SUNSTATION").GetState() == ShipLogEntry.State.Hidden)
            {
                return "Object orbiting the sun";
            }
            return "Sun Station";
        }
    }

    public class QuantumMoonDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string GetNewName()
        {
            if (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("QUANTUM_MOON").GetState() == ShipLogEntry.State.Hidden)
            {
                return "White cloudy moon";
            }
            return "The Quantum Moon";
        }

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;
            if (Locator.GetQuantumMoon().GetStateIndex() == 5)
            {
                reason = $"Cannot locate {GetName()}.";
                return false;
            }
            return true;
        }

        public override bool CanLand() => true;
    }

    public class OPCDestination(string name, string path, float innerRadius, float outerRadius) : FloatingDestination(name, path, innerRadius, outerRadius)
    {
        public override string GetNewName()
        {
            if (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("ORBITAL_PROBE_CANNON").GetState() == ShipLogEntry.State.Hidden)
            {
                return "Giant's Deep Orbital Flash";
            }
            return "Orbital Probe Cannon";
        }
    }

    public class WhiteHoleStationDestination(string name, string path, float innerRadius, float outerRadius) : FloatingDestination(name, path, innerRadius, outerRadius)
    {
        public override string GetNewName()
        {
            if (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("WHITE_HOLE_STATION").GetState() == ShipLogEntry.State.Hidden)
            {
                return "White spot";
            }
            return "White Hole Station";
        }
    }

    public class MapSatelliteDestination(string name, string path, float innerRadius, float outerRadius) : FloatingDestination(name, path, innerRadius, outerRadius)
    {
        public override string GetNewName()
        {
            if (!PlayerData.KnowsFrequency(SignalFrequency.Radio))
            {
                return "Red spot";
            }
            return "Hearthian Map Satellite";
        }
    }

    public class StrangerDestination(string name, string path, bool lightSide, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        readonly bool lightSide = lightSide;

        public override string GetNewName()
        {
            if (Locator.GetShipLogManager() && Locator.GetShipLogManager().IsFactRevealed("IP_RING_WORLD_X1"))
            {
                return $"The Stranger";
            }
            if (!IsAvailable(out _)) {
                return "Nothing";
            }
            return "Dark shadow over the sun";
        }

        Transform mapSatellite;

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            var ringWorld = Locator.GetAstroObject(AstroObject.Name.RingWorld)?.transform;
            var sun = Locator.GetAstroObject(AstroObject.Name.Sun)?.transform;
            var ship = Locator.GetShipTransform();

            if (!sun || !ship)
            {
                reason = "Game not loaded yet.";
                return false;
            }

            if (!ringWorld)
            {
                reason = "This is nothing.";
                return false;
            }

            var eclipseDot = Vector3.Dot((sun.position - ringWorld.position).normalized, (ringWorld.position - ship.position).normalized);

            // If we have not discovered the Stranger yet, check if the ship is near the map satellite and the eclipse is visible
            if (!Locator.GetShipLogManager() || !Locator.GetShipLogManager().IsFactRevealed("IP_RING_WORLD_X1"))
            {
                if (!mapSatellite)
                {
                    mapSatellite = GameObject.Find("HearthianMapSatellite_Body")?.transform;
                    if (!mapSatellite)
                    {
                        reason = "Game not loaded yet.";
                        return false;
                    }
                }

                var isEclipseVisible = eclipseDot > 0.96f;
                var isNearMapSatellite = Vector3.Distance(ship.position, mapSatellite.position) < 100f;

                if (!isNearMapSatellite || !isEclipseVisible || !ringWorld)
                {
                    reason = "This is nothing.";
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

    public class ShuttleDestination(string name, NomaiShuttleController.ShuttleID shuttleID, float innerRadius, float outerRadius) : Destination(name, innerRadius, outerRadius)
    {
        readonly NomaiShuttleController.ShuttleID shuttleID = shuttleID;
        ReferenceFrameVolume rfv;
        NomaiShuttleController controller;

        public override bool CanLand() => false;

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;
            if (!isDiscovered())
            {
                reason = "This is nothing.";
                return false;
            }
            if (!controller)
            {
                reason = "Shuttle not found.";
                return false;
            }
            if (controller._shuttleBody.IsSuspended())
            {
                reason = "Shuttle has not yet been recalled.";
                return false;
            }
            return true;
        }

        private bool isDiscovered() => Locator.GetShipLogManager() &&
                ((Locator.GetShipLogManager().GetFact("BH_GRAVITY_CANNON_X2").IsRevealed() && shuttleID == NomaiShuttleController.ShuttleID.BrittleHollowShuttle)
                || (Locator.GetShipLogManager().GetFact("CT_GRAVITY_CANNON_X2").IsRevealed() && shuttleID == NomaiShuttleController.ShuttleID.HourglassShuttle));

        public override string GetNewName()
        {
            if (!isDiscovered())
            {
                return "Nothing";
            }
            if (shuttleID == NomaiShuttleController.ShuttleID.BrittleHollowShuttle) {
                return "Brittle Hollow Shuttle";
            }
            return "Ember Twin Shuttle";
        }

        public override ReferenceFrame GetReferenceFrame()
        {
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

        public override void SetUp()
        {
            controller = GameObject.FindObjectsOfType<NomaiShuttleController>().FirstOrDefault(c => c.GetID() == shuttleID);
            rfv = controller._shuttleBody.transform.Find("RF_Volume").GetComponent<ReferenceFrameVolume>();
        }
    }

    public class ShipDestination() : FixedDestination("Ship", "Ship_Body/Volumes/RFVolume", 0f, 20f)
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

        public override string GetNewName() => StandaloneProfileManager.SharedInstance.currentProfile?.profileName ?? "Player";

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
            if (rfv == null) return null;
            return rfv.GetReferenceFrame();
        }

        public override bool PlayerIsAt() => false;
        public override bool ShipIsAt() => false;

        public override float GetDistanceToPlayer() => 0f;
        public override float GetDistanceToShip() => Vector3.Distance(Locator.GetPlayerBody().GetPosition(), Locator.GetShipBody().GetPosition());

        public override void SetUp()
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
    }

    public class TargetedDestination() : Destination("Targeted Body", 0f, 100f)
    {
        public override bool CanLand() => Destination()?.CanLand() ?? false;

        public override ReferenceFrame GetReferenceFrame()
        {
            var rfv = Locator._rfTracker?.GetReferenceFrame();
            if (rfv == null) return null;
            return rfv;
        }

        public string GetDestinationName() => Destinations.GetByType<TargetedDestination>()?.Destination()?.GetName()
                ?? (string.IsNullOrWhiteSpace(GetReferenceFrame()?.GetHUDDisplayName())
                ? "A destination" : GetReferenceFrame().GetHUDDisplayName());

        public override bool IsAvailable(out string reason)
        {
            if (GetReferenceFrame() == null)
            {
                reason = "No destination is currently targeted.";
                return false;
            }
            if (Destination() is ShipDestination)
            {
                reason = "Ship cannot autopilot to itself.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public override bool PlayerIsAt() => Destination()?.PlayerIsAt() ?? (GetReferenceFrame().GetPosition() - Locator.GetPlayerBody().GetPosition()).magnitude < 100;
        public override bool ShipIsAt() => Destination()?.ShipIsAt() ?? (GetReferenceFrame().GetPosition() - Locator.GetShipBody().GetPosition()).magnitude < 100;

        public override float GetDistanceToShip()
        {
            var shipPos = Locator.GetShipBody().GetPosition();
            var destPos = GetReferenceFrame()?.GetPosition() ?? shipPos;
            return Vector3.Distance(destPos, shipPos);
        }

        public override float GetDistanceToPlayer()
        {
            var playerPos = Locator.GetPlayerBody().GetPosition();
            var destPos = GetReferenceFrame()?.GetPosition() ?? playerPos;
            return Vector3.Distance(destPos, playerPos);
        }

        public Destination Destination() {
            var destination = Destinations.GetByReferenceFrame(GetReferenceFrame());
            if (destination == this) 
                destination = null;
            return destination;
        }
    }
}

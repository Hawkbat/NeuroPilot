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
            new StrangerDestination("The Stranger (Dark Side Docking Bay)", "RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_DarkSideDockingBay", false, 200f, 300f),
            new StrangerDestination("The Stranger (Light Side Docking Bay)", "RingWorld_Body/Sector_RingWorld/Volumes_RingWorld/RFVolume_IP_LightSideDockingBay", true, 200f, 300f),

            new FixedDestination("The Sun", "Sun_Body/RFVolume_SUN", 3000f, 4000f),
            new SunStationDestination("Sun Station (Warp Module)", "SunStation_Body/Sector_SunStation/Sector_WarpModule/Volumes_WarpModule/RFVolume", 50f, 150f),
            new SunStationDestination("Sun Station (Control Module)", "SunStation_Body/Sector_SunStation/Sector_ControlModule/Volumes/RFVolume", 50f, 150f),

            new PlanetoidDestination("Ash Twin", "TowerTwin_Body/Volumes_TowerTwin/RFVolume", 380f, 1000f),
            new PlanetoidDestination("Ember Twin", "CaveTwin_Body/Volumes_CaveTwin/RFVolume", 380f, 1000f),

            new PlanetoidDestination("Timber Hearth", "TimberHearth_Body/RFVolume_TH", 400f, 1000f),
            new PlanetoidDestination("The Attlerock", "Moon_Body/RFVolume_THM", 130f, 350f),

            new PlanetoidDestination("Brittle Hollow", "BrittleHollow_Body/RFVolume_BH", 600f, 1000f),
            new FixedDestination("Hollow's Lantern", "VolcanicMoon_Body/RFVolume_VM", 200f, 500f),

            new PlanetoidDestination("Giant's Deep", "GiantsDeep_Body/RFVolume_GD", 950f, 2500f),
            new OPCDestination("Orbital Probe Cannon", "OrbitalProbeCannon_Body/RFVolume_OrbitalProbeCannon", 200f, 400f),
            new ProbeDestination("Probe", "NomaiProbe_Body/RFVolume", 100f, 500f),

            new FixedDestination("Dark Bramble", "DarkBramble_Body/RFVolume_DB", 950f, 1800f),

            new QuantumMoonDestination("The Quantum Moon", "QuantumMoon_Body/Volumes/RFVolume", 110f, 500f),

            new PlanetoidDestination("The Interloper", "Comet_Body/RFVolume_CO", 300f, 600f),

            new WhiteHoleStationDestination("White Hole Station", "WhiteholeStation_Body/RFVolume_WhiteholeStation", 100f, 300f),

            new MapSatelliteDestination("Hearthian Map Satellite", "HearthianMapSatellite_Body/RFVolume_HMS", 50f, 300f),

            new FixedDestination("Secret Satellite", "BackerSatellite_Body/RFVolume_BS", 200f, 400f),

            new ShuttleDestination("Ember Twin Shuttle", "Comet_Body/Prefab_NOM_Shuttle/Shuttle_Body/RFVolume", NomaiShuttleController.ShuttleID.HourglassShuttle, 50f, 200f),
            new ShuttleDestination("Brittle Hollow Shuttle", "QuantumMoon_Body/Sector_QuantumMoon/QuantumShuttle/Prefab_NOM_Shuttle/Shuttle_Body/RFVolume", NomaiShuttleController.ShuttleID.BrittleHollowShuttle, 50f, 200f),

            new ShipDestination(),
            new PlayerDestination(),
            new TargetedDestination(),
            new UnlistedDestination()
        ];

        static readonly Dictionary<Destination, bool> previousAvailability = [];
        static readonly Dictionary<Destination, string> previousNames = [];

        public static IEnumerable<Destination> GetAll() => destinations;
        public static IEnumerable<Destination> GetAllValid() => GetAll().Where(d => d.IsAvailable(out string reason));

        public static IEnumerable<string> GetAllNames() => destinations.Select(d => d.Name);
        public static IEnumerable<string> GetAllValidNames() => GetAllValid().Select(d => d.Name);

        public static Destination GetByName(string name)
            => GetAllValid().Concat(GetAll()).FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static Destination GetByReferenceFrame(ReferenceFrame rf) //TODO Check all usages for redirecting unlisted
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
            var destination = GetByType<UnlistedDestination>();
            destination.SetReferenceFrame(rf);
            return destination;
        }

        public static T GetByType<T>() where T : class => GetAllValid().Concat(GetAll()).OfType<T>().FirstOrDefault();

        public static Destination GetShipLocation()
            => GetAllValid().Where(d => d.ShipIsAt()).OrderBy(d => d.GetDistanceToShip()).FirstOrDefault();
        public static Destination GetPlayerLocation()
            => GetAllValid().Where(d => d.PlayerIsAt()).OrderBy(d => d.GetDistanceToPlayer()).FirstOrDefault();

        public static void SetUp()
        {
            foreach (var d in destinations) d.SetUp();
        }

        public static bool CheckForChanges()
        {
            if (LoadManager.GetCurrentScene() != OWScene.SolarSystem)
                return false;

            var anyChanged = false;
            foreach (var d in destinations)
            {
                var name = d.Name;
                var available = d.IsAvailable(out _);
                if (previousNames.TryGetValue(d, out string previousName))
                {
                    if (name != previousName)
                        anyChanged = true;
                }
                previousNames[d] = name;
                if (previousAvailability.TryGetValue(d, out bool wasAvailable))
                {
                    if (available != wasAvailable)
                        anyChanged = true;
                }
                previousAvailability[d] = available;
            }
            return anyChanged;
        }
    }

    public abstract class Destination(string name, float innerRadius, float outerRadius) // TODO can one of these just be made autopilotRadius
    {
        public virtual string Name => name;
        public float InnerRadius => innerRadius;
        public float OuterRadius => outerRadius;

        public virtual bool CanOrbit() => false;
        public virtual bool CanLand() => false;
        public override string ToString() => Name;

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

        public virtual bool ShipIsAt() => GetDistanceToShip() <= OuterRadius;
        public virtual bool PlayerIsAt() => GetDistanceToPlayer() <= InnerRadius;

        public virtual float GetDistanceToShip()
        {
            var shipPos = Locator.GetShipBody().GetPosition();
            var destPos = GetReferenceFrame()?.GetPosition() ?? shipPos;
            return Vector3.Distance(destPos, shipPos);
        }

        public virtual float GetDistanceToPlayer()
        {
            var playerPos = Locator.GetPlayerBody().GetPosition();
            var destPos = GetReferenceFrame()?.GetPosition() ?? playerPos;
            return Vector3.Distance(destPos, playerPos);
        }

        public virtual Transform GetTransform()
            => GetReferenceFrame()?.GetOWRigidBody().transform;

        public virtual IEnumerable<(string, Transform)> GetLocations()
            => Locations.ByDestination(this);

        public virtual void SetUp() { }
    }

    public class FixedDestination(string name, string path, float innerRadius, float outerRadius) : Destination(name, innerRadius, outerRadius)
    {
        protected readonly string path = path;

        protected ReferenceFrameVolume rfv;

        public override ReferenceFrame GetReferenceFrame() => rfv?.GetReferenceFrame();

        public override void SetUp()
        {
            var parts = path.Split('/');
            var go = GameObject.Find(parts[0]);
            if (go == null)
            {
                NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{Name}' at path '{path}' does not exist. Missing part: {parts[0]}", MessageType.Warning);
                return;
            }
            for (var i = 1; i < parts.Length; i++)
            {
                var t = go.transform.Find(parts[i]);
                if (t == null)
                {
                    NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{Name}' at path '{path}' does not exist. Missing part: {parts[i]}", MessageType.Warning);
                    return;
                }
                go = t.gameObject;
            }

            rfv = go.GetComponent<ReferenceFrameVolume>();
            if (!rfv) NeuroPilot.instance.ModHelper.Console.WriteLine($"Destination '{Name}' at path '{path}' does not have a ReferenceFrameVolume component.", MessageType.Warning);
        }
    }

    public class StrangerDestination(string name, string path, bool lightSide, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        readonly bool lightSide = lightSide;

        public override string Name =>
            Locator.GetShipLogManager() && Locator.GetShipLogManager().IsFactRevealed("IP_RING_WORLD_X1")
                ? "The Stranger"
                : "Dark shadow over the sun";

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            var ringWorld = Locator._ringWorld?.transform;
            var sun = Locator.GetSunTransform();
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
                var mapSatellite = Locations.GetMapSatellite();
                if (!mapSatellite)
                {
                    reason = "Game not loaded yet.";
                    return false;
                }

                var isEclipseVisible = eclipseDot > 0.96f;
                var isNearMapSatellite = Vector3.Distance(ship.position, mapSatellite.position) < 200f;

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

    public class SunStationDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("S_SUNSTATION").GetState() == ShipLogEntry.State.Hidden)
                ? "Object orbiting the sun"
                : "Sun Station";
    }

    public class PlanetoidDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override bool CanLand() => true;
        public override bool CanOrbit() => true;
    }

    public class OPCDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("ORBITAL_PROBE_CANNON").GetState() == ShipLogEntry.State.Hidden)
                ? "Giant's Deep Orbital Flash"
                : "Orbital Probe Cannon";
    }

    public class ProbeDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("ORBITAL_PROBE_CANNON").GetState() == ShipLogEntry.State.Hidden)
                ? "Fired blue thing"
                : "Probe";

        public override ReferenceFrame GetReferenceFrame() => rfv?.GetReferenceFrame()?.GetOWRigidBody() ? rfv?.GetReferenceFrame() : null;

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            if (GetDistanceToShip() > 50_000f)
            {
                reason = $"{Name} is too far.";
                return false;
            }
            return true;
        }

        public override void SetUp()
        {
            var probeBody = GameObject.Find("NomaiProbe_Body");
            if (probeBody) Locations.AddReferenceFrame(probeBody, 300, 5, 15000f);
            else NeuroPilot.instance.ModHelper.Console.WriteLine("NomaiProbe_Body not found!", MessageType.Error);
            base.SetUp();
        }
    }

    public class QuantumMoonDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("QUANTUM_MOON").GetState() == ShipLogEntry.State.Hidden)
                ? "White cloudy moon"
                : "The Quantum Moon";

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            if ((Locator.GetQuantumMoon()?.GetStateIndex() ?? 5) == 5)
            {
                reason = $"Cannot locate {Name}.";
                return false;
            }
            return true;
        }

        public override bool CanLand() => true;
    }

    public class WhiteHoleStationDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            (!Locator.GetShipLogManager() || Locator.GetShipLogManager().GetEntry("WHITE_HOLE_STATION").GetState() == ShipLogEntry.State.Hidden)
                ? "White spot"
                : "White Hole Station";
    }

    public class MapSatelliteDestination(string name, string path, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        public override string Name =>
            !PlayerData.KnowsFrequency(SignalFrequency.Radio)
                ? "Red spot"
                : "Hearthian Map Satellite";
    }

    public class ShuttleDestination(string name, string path, NomaiShuttleController.ShuttleID shuttleID, float innerRadius, float outerRadius) : FixedDestination(name, path, innerRadius, outerRadius)
    {
        readonly NomaiShuttleController.ShuttleID shuttleID = shuttleID;
        NomaiShuttleController controller;

        public override string Name =>
            shuttleID == NomaiShuttleController.ShuttleID.BrittleHollowShuttle
            ? "Brittle Hollow Shuttle"
            : "Ember Twin Shuttle";

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

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
            if (!Locator.GetShipLogManager() ||
                (!Locator.GetShipLogManager().GetFact("BH_GRAVITY_CANNON_X2").IsRevealed() && shuttleID == NomaiShuttleController.ShuttleID.BrittleHollowShuttle)
                || (!Locator.GetShipLogManager().GetFact("CT_GRAVITY_CANNON_X2").IsRevealed() && shuttleID == NomaiShuttleController.ShuttleID.HourglassShuttle))
            {
                reason = "Shuttle has not been discovered.";
                return false;
            }
            return true;
        }

        public override void SetUp()
        {
            base.SetUp();
            controller = GameObject.FindObjectsOfType<NomaiShuttleController>().FirstOrDefault(c => c.GetID() == shuttleID);
            rfv = controller._shuttleBody.transform.Find("RF_Volume").GetComponent<ReferenceFrameVolume>();
        }
    }

    public class ShipDestination() : FixedDestination("Ship", "Ship_Body/Volumes/RFVolume", 0f, 0f)
    {
        public override bool IsAvailable(out string reason)
        {
            reason = "Ship cannot autopilot to itself.";
            return false;
        }

        public override bool PlayerIsAt() => PlayerState.IsInsideShip(); // TODO should allow player is at ship?
        public override bool ShipIsAt() => false;
        public override float GetDistanceToShip() => 0f;
    }

    public class PlayerDestination() : FixedDestination("Player", "Player_Body/RFVolume", 0f, 0f)
    {
        public override string Name =>
            StandaloneProfileManager.SharedInstance.currentProfile?.profileName ?? "Player";

        public override bool IsAvailable(out string reason)
        {
            if (!base.IsAvailable(out reason)) return false;

            if (PlayerState.IsInsideShip())
            {
                reason = "Player is inside the ship.";
                return false;
            }

            if (PlayerState.InBrambleDimension() && EnhancedAutoPilot.GetInstance() && !EnhancedAutoPilot.GetInstance().InPlayerBrambleDimension())
            {
                reason = "Player is inside another part of Dark Bramble and cannot be located.";
                return false;
            }

            if (PlayerState.InCloakingField() && !Locator.GetCloakFieldController().isShipInsideCloak)
            {
                reason = "Player is inside a cloaking field and cannot be located. Enter the cloaking field first.";
                return false;
            }

            return true;
        }

        public override bool PlayerIsAt() => false;
        public override bool ShipIsAt() => false;

        public override float GetDistanceToPlayer() => 0f;

        public override void SetUp()
        {
            var player = GameObject.FindGameObjectWithTag("Player").gameObject;
            Locations.AddReferenceFrame(player, 20, 0, 0);
            base.SetUp();
        }
    }

    public abstract class FloatingDestination(string name, float innerRadius, float outerRadius) : Destination(name, innerRadius, outerRadius)
    {
        public override bool PlayerIsAt() => Destination()?.PlayerIsAt() ?? (GetReferenceFrame().GetPosition() - Locator.GetPlayerBody().GetPosition()).magnitude < OuterRadius;
        public override bool ShipIsAt() => Destination()?.ShipIsAt() ?? (GetReferenceFrame().GetPosition() - Locator.GetShipBody().GetPosition()).magnitude < OuterRadius;

        public string GetDestinationName() => Destination()?.Name
            ?? (string.IsNullOrWhiteSpace(GetReferenceFrame()?.GetHUDDisplayName())
            ? "A destination" : GetReferenceFrame().GetHUDDisplayName());

        public Destination Destination()
        {
            var rf = GetReferenceFrame();
            if (rf == null) return null;

            foreach (var dest in Destinations.GetAll())
            {
                if (dest is FloatingDestination)
                    continue;

                var destRF = dest.GetReferenceFrame();
                if (destRF != null && destRF == rf)
                {
                    return dest;
                }
            }
            return null;
        }
    }

    public class TargetedDestination() : FloatingDestination("Targeted Body", 0f, 100f)
    {
        public override bool CanLand() => Destination()?.CanLand() ?? false;

        public override ReferenceFrame GetReferenceFrame()
        {
            var rfv = Locator._rfTracker?.GetReferenceFrame();
            if (rfv == null) return null;
            return rfv;
        }

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

            if (PlayerState.InBrambleDimension() && EnhancedAutoPilot.GetInstance() && !EnhancedAutoPilot.GetInstance().InPlayerBrambleDimension())
            {
                reason = "Player is inside another part of Dark Bramble and their targeted body cannot be located.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    public class UnlistedDestination() : FloatingDestination("A Destination", 0f, 100f)
    {
        ReferenceFrame referenceFrame;
        public override string Name => GetDestinationName();
        public override bool IsAvailable(out string reason)
        {
            reason = "Unlisted destination.";
            return false;
        }
        public override bool CanLand() => Destination()?.CanLand() ?? false;

        public void SetReferenceFrame(ReferenceFrame rf)
        {
            referenceFrame = rf;
        }

        public override ReferenceFrame GetReferenceFrame() => referenceFrame;
    }
}

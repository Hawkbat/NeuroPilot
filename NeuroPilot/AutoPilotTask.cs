using System;
using UnityEngine;

namespace NeuroPilot
{
    public abstract class AutoPilotTask(Destination destination, string actionVerb)
    {
        public Destination Destination => destination ?? throw new ArgumentNullException(nameof(destination));

        public override string ToString() => $"{actionVerb} {Destination}";
    }

    public sealed class TravelTask(Destination destination) : AutoPilotTask(destination, "travel to");
    public sealed class TakeOffTask(Destination destination) : AutoPilotTask(destination, "take off from");
    public sealed class LandingTask(Destination destination) : AutoPilotTask(destination, "land at");
    public sealed class EvadeTask(Destination destination) : AutoPilotTask(destination, "evade");
    public sealed class CrashTask(Destination destination) : AutoPilotTask(destination, "travel to");
    public sealed class OrbitToLocationTask(Destination destination, string locationName, Transform locationTransform) : AutoPilotTask(destination, "orbit")
    {
        public string LocationName => locationName;
        public Transform LocationTransform => locationTransform;

        public override string ToString() => $"orbit {Destination} to {locationName}";
    }
}



namespace NeuroPilot
{
    public abstract class AutoPilotTask
    {

    }

    public class TravelTask(Destination destination) : AutoPilotTask
    {
        public readonly Destination destination = destination;

        public override string ToString() => $"travel to {destination}";
    }

    public class TakeOffTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"take off from {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class LandingTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"land at {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class EvadeTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"evade {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class CrashTask(Destination destination) : AutoPilotTask
    {
        public readonly Destination destination = destination;

        public override string ToString() => $"travel to {destination}";
    }
}

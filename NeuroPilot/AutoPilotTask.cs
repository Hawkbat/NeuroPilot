

namespace NeuroPilot
{
    public abstract class AutoPilotTask
    {

    }

    public class TravelTask(Destination destination) : AutoPilotTask
    {
        public readonly Destination destination = destination;

        public override string ToString() => $"Autopilot engaged to travel to {destination}";
    }

    public class TakeOffTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Autopilot engaged to take off from {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class LandingTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Autopilot engaged to land at {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class EvadeTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Autopilot engaged to evade {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }
}

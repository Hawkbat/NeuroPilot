using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroPilot
{
    public abstract class AutoPilotTask
    {

    }

    public class TravelTask(Destination destination) : AutoPilotTask
    {
        public readonly Destination destination = destination;

        public override string ToString() => $"Traveling to {destination}";
    }

    public class TakeOffTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Taking off from {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class LandingTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Landing at {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }

    public class EvadeTask(ReferenceFrame location) : AutoPilotTask
    {
        public readonly ReferenceFrame location = location;

        public override string ToString() => $"Evading {Destinations.GetByReferenceFrame(location)?.GetName() ?? location.GetHUDDisplayName() ?? "Unknown location"}";
    }
}

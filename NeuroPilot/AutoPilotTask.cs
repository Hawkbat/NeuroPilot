using System;

namespace NeuroPilot
{
    public abstract class AutoPilotTask(Destination destination, string actionVerb)
    {
        public Destination Destination { get; } = destination ?? throw new ArgumentNullException(nameof(destination));

        private readonly string _actionVerb = actionVerb;

        public override string ToString() => $"{_actionVerb} {Destination}";
    }

    public sealed class TravelTask(Destination destination) : AutoPilotTask(destination, "travel to");
    public sealed class TakeOffTask(Destination destination) : AutoPilotTask(destination, "take off from");
    public sealed class LandingTask(Destination destination) : AutoPilotTask(destination, "land at");
    public sealed class EvadeTask(Destination destination) : AutoPilotTask(destination, "evade");
    public sealed class CrashTask(Destination destination) : AutoPilotTask(destination, "travel to");
}
